using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;

namespace RfidVehicleAccess.Services;

public sealed class WeightChangedEventArgs(
    decimal weightKg,
    string rawData,
    DateTimeOffset receivedAt) : EventArgs
{
    public decimal WeightKg { get; } = weightKg;
    public string RawData { get; } = rawData;
    public DateTimeOffset ReceivedAt { get; } = receivedAt;
}

public sealed class WeighbridgeConnectionChangedEventArgs(
    bool isConnected,
    string status) : EventArgs
{
    public bool IsConnected { get; } = isConnected;
    public string Status { get; } = status;
}

public sealed class WeighbridgeApplyResult(bool success, string message)
{
    public bool Success { get; } = success;
    public string Message { get; } = message;
}

/// <summary>
/// Owns the single physical weighbridge serial connection used by iAWS.
/// Supports line-oriented and framed weighbridge data from common indicators.
/// Universal mode auto-detects prefixed/unit-tagged values and plain numeric frames.
/// </summary>
public sealed partial class WeighbridgeService : BackgroundService, IDisposable
{
    private readonly AppOptions _options;
    private readonly AppLogger _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly object _readSync = new();
    private readonly StringBuilder _readBuffer = new();

    private SerialPort? _port;
    private bool _disposed;
    private decimal _currentWeightKg;
    private string _currentStatus = "Weight bridge starting...";
    private bool _isConnected;
    private decimal? _stableCandidate;
    private int _stableCount;

    [GeneratedRegex(@"^\s*(?<prefix>[A-Za-z]+)\s*(?<weight>[+-]?\d+(?:[.,]\d+)?)\s*(?<unit>[A-Za-z]+)?\s*$", RegexOptions.Compiled)]
    private static partial Regex WeightLineRegex();

    [GeneratedRegex(@"(?<weight>[+-]?\d+(?:[.,]\d+)?)\s*(?<unit>kg|kgs|kilograms?|g|grams?|t|ton|tons|tonne|tonnes)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UnitWeightRegex();

    [GeneratedRegex(@"[+-]?\d+(?:[.,]\d+)?", RegexOptions.Compiled)]
    private static partial Regex AnyNumberRegex();

    [GeneratedRegex(@"(?<=\d)\s+(?=\d)|(?<=[+-])\s+(?=\d)", RegexOptions.Compiled)]
    private static partial Regex NumericWhitespaceRegex();

    [GeneratedRegex(@"^[+-]?\d{6}$", RegexOptions.Compiled)]
    private static partial Regex FixedSixDigitFrameRegex();

    [GeneratedRegex(@"^,\s*[+-]?\d{1,8}$", RegexOptions.Compiled)]
    private static partial Regex CommaPrefixedFrameRegex();

    public WeighbridgeService(AppOptions options, AppLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public event EventHandler<WeightChangedEventArgs>? WeightChanged;
    public event EventHandler<WeighbridgeConnectionChangedEventArgs>? ConnectionChanged;

    public decimal CurrentWeightKg => _currentWeightKg;
    public string CurrentStatus => _currentStatus;
    public bool IsConnected => _isConnected;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Hardware.SimulationEnabled)
                {
                    PublishConnection(true, "Simulation mode - weighbridge COM disabled");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                if (!_options.Weighbridge.Enabled)
                {
                    ClosePort();
                    PublishConnection(false, "Weight bridge disabled");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                if (_port?.IsOpen != true)
                {
                    await ConnectAsync(stoppingToken);
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(1, _options.Hardware.ReconnectSeconds)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ClosePort();
                PublishConnection(false, $"Weight bridge connection failed: {ex.Message}");
                await _logger.StatusAsync(
                    $"Weight bridge connection failed: {ex.Message}",
                    CancellationToken.None);

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(Math.Max(1, _options.Hardware.ReconnectSeconds)),
                        stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        ClosePort();
        PublishConnection(false, "Weight bridge stopped");
    }

    public async Task<WeighbridgeApplyResult> ApplySettingsAsync(
        WeighbridgeOptions settings,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            CopySettings(settings, _options.Weighbridge);
            _stableCandidate = null;
            _stableCount = 0;
            ClosePort();

            if (_options.Hardware.SimulationEnabled)
            {
                const string simulated = "Weight bridge settings saved (simulation mode).";
                PublishConnection(true, simulated);
                return new WeighbridgeApplyResult(true, simulated);
            }

            if (!_options.Weighbridge.Enabled)
            {
                const string disabled = "Weight bridge settings saved; weighbridge is disabled.";
                PublishConnection(false, disabled);
                return new WeighbridgeApplyResult(true, disabled);
            }

            try
            {
                await ConnectAsync(cancellationToken);
                return new WeighbridgeApplyResult(true, _currentStatus);
            }
            catch (Exception ex)
            {
                ClosePort();
                var message = $"Weight bridge settings saved, but COM connection failed: {ex.Message}";
                PublishConnection(false, message);
                return new WeighbridgeApplyResult(false, message);
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public void SimulateWeight(decimal weightKg)
    {
        if (!_options.Hardware.SimulationEnabled)
        {
            return;
        }

        PublishWeight(Math.Max(0m, weightKg), $"SIM {weightKg:0.###} kg");
    }

    public bool TryParseWeight(string rawLine, out decimal weightKg)
    {
        weightKg = 0m;
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return false;
        }

        return _options.Weighbridge.UniversalFormatEnabled
            ? TryParseUniversalWeight(rawLine, out weightKg)
            : TryParseLegacyWeight(rawLine, out weightKg);
    }

    private bool TryParseLegacyWeight(string rawLine, out decimal weightKg)
    {
        weightKg = 0m;
        var match = WeightLineRegex().Match(rawLine.Trim());
        if (!match.Success)
        {
            return false;
        }

        var expectedPrefix = _options.Weighbridge.DataPrefix?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(expectedPrefix) &&
            !string.Equals(match.Groups["prefix"].Value, expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var unit = match.Groups["unit"].Value;
        var expectedUnit = _options.Weighbridge.UnitText?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(unit) &&
            !string.IsNullOrWhiteSpace(expectedUnit) &&
            !string.Equals(unit, expectedUnit, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryParseNumericToken(match.Groups["weight"].Value, out var parsed))
        {
            return false;
        }

        weightKg = Math.Max(0m, ConvertToKilograms(parsed, unit, expectedUnit));
        return true;
    }

    private bool TryParseUniversalWeight(string rawLine, out decimal weightKg)
    {
        weightKg = 0m;

        var normalized = rawLine
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\u0002", string.Empty, StringComparison.Ordinal)
            .Replace("\u0003", string.Empty, StringComparison.Ordinal)
            .Trim();
        normalized = NumericWhitespaceRegex().Replace(normalized, string.Empty);
        if (normalized.Length == 0)
        {
            return false;
        }

        // Prefer an explicit unit because it is the least ambiguous format.
        var unitMatch = UnitWeightRegex().Match(normalized);
        if (unitMatch.Success &&
            TryParseNumericToken(unitMatch.Groups["weight"].Value, out var unitValue))
        {
            weightKg = Math.Max(
                0m,
                ConvertToKilograms(
                    unitValue,
                    unitMatch.Groups["unit"].Value,
                    _options.Weighbridge.UnitText));
            return true;
        }

        // Otherwise accept common raw numeric/status formats such as:
        // 000020, ", 00", "ST,GS,+000020", "WT:000020".
        var numericMatches = AnyNumberRegex().Matches(normalized);
        if (numericMatches.Count == 0)
        {
            return false;
        }

        var selected = numericMatches[numericMatches.Count - 1].Value;
        if (!TryParseNumericToken(selected, out var value))
        {
            return false;
        }

        weightKg = Math.Max(
            0m,
            ConvertToKilograms(value, string.Empty, _options.Weighbridge.UnitText));
        return true;
    }

    private static bool TryParseNumericToken(string token, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var normalized = token.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            var commaIndex = normalized.LastIndexOf(',');
            var dotIndex = normalized.LastIndexOf('.');
            if (commaIndex > dotIndex)
            {
                normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal)
                    .Replace(',', '.');
            }
            else
            {
                normalized = normalized.Replace(",", string.Empty, StringComparison.Ordinal);
            }
        }
        else if (normalized.Contains(','))
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static decimal ConvertToKilograms(
        decimal value,
        string? incomingUnit,
        string? defaultUnit)
    {
        var unit = string.IsNullOrWhiteSpace(incomingUnit)
            ? defaultUnit?.Trim() ?? "kg"
            : incomingUnit.Trim();

        return unit.ToLowerInvariant() switch
        {
            "g" or "gram" or "grams" => value / 1000m,
            "t" or "ton" or "tons" or "tonne" or "tonnes" => value * 1000m,
            _ => value
        };
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var serial = _options.Weighbridge.Serial;
        if (string.IsNullOrWhiteSpace(serial.PortName))
        {
            throw new InvalidOperationException("Weight bridge COM port is empty.");
        }

        ClosePort();
        var port = new SerialPort(
            serial.PortName.Trim(),
            serial.BaudRate,
            ParseParity(serial.Parity),
            serial.DataBits,
            ParseStopBits(serial.StopBits))
        {
            NewLine = DecodeEscapes(_options.Hardware.LineTerminator),
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            Encoding = Encoding.ASCII
        };

        port.DataReceived += OnDataReceived;
        port.Open();
        _port = port;

        var status = $"Weight bridge connected: {port.PortName} @ {port.BaudRate}";
        PublishConnection(true, status);
        await _logger.StatusAsync(status, cancellationToken);
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs eventArgs)
    {
        try
        {
            if (sender is not SerialPort port || !port.IsOpen)
            {
                return;
            }

            var text = port.ReadExisting();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            ProcessIncomingText(text);
        }
        catch (Exception ex)
        {
            PublishConnection(false, $"Weight bridge read error: {ex.Message}");
            _ = _logger.StatusAsync($"Weight bridge read error: {ex.Message}");
        }
    }

    private void ProcessIncomingText(string text)
    {
        List<string> lines = [];
        lock (_readSync)
        {
            _readBuffer.Append(
                text.Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace('\r', '\n')
                    .Replace('\u0003', '\n')
                    .Replace("\u0002", string.Empty, StringComparison.Ordinal)
                    .Replace("\0", string.Empty, StringComparison.Ordinal));
            while (true)
            {
                var buffer = _readBuffer.ToString();
                var newlineIndex = buffer.IndexOf('\n');
                if (newlineIndex < 0)
                {
                    break;
                }

                var line = buffer[..newlineIndex].Trim();
                _readBuffer.Clear();
                _readBuffer.Append(buffer[(newlineIndex + 1)..]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            // Some indicators send complete frames without CR/LF. Accept only formats
            // that give us a reliable frame boundary: an explicit unit, a fixed six-digit
            // numeric frame (for example 000020), or a comma-prefixed numeric frame.
            // This avoids treating a partial chunk such as "wn009" as a complete value.
            if (_readBuffer.Length > 0 && _readBuffer.Length < 64)
            {
                var candidate = _readBuffer.ToString().Trim();
                var expectedUnit = _options.Weighbridge.UnitText?.Trim() ?? string.Empty;
                var compactCandidate = NumericWhitespaceRegex().Replace(candidate, string.Empty);
                var looksComplete = UnitWeightRegex().IsMatch(candidate) ||
                                    FixedSixDigitFrameRegex().IsMatch(compactCandidate) ||
                                    CommaPrefixedFrameRegex().IsMatch(candidate) ||
                                    (!string.IsNullOrWhiteSpace(expectedUnit) &&
                                     candidate.EndsWith(expectedUnit, StringComparison.OrdinalIgnoreCase));

                if (looksComplete && TryParseWeight(candidate, out _))
                {
                    lines.Add(candidate);
                    _readBuffer.Clear();
                }
            }
        }

        foreach (var line in lines)
        {
            if (!TryParseWeight(line, out var weightKg))
            {
                continue;
            }

            RegisterStableReading(weightKg, line);
        }
    }

    private void RegisterStableReading(decimal weightKg, string rawLine)
    {
        var required = Math.Max(1, _options.Weighbridge.StableReadCount);
        if (_stableCandidate.HasValue && Math.Abs(_stableCandidate.Value - weightKg) <= 0.5m)
        {
            _stableCount++;
        }
        else
        {
            _stableCandidate = weightKg;
            _stableCount = 1;
        }

        // Always update the visible live value. The stable-read setting only gates
        // the event used by the process coordinator.
        _currentWeightKg = weightKg;
        if (_stableCount >= required)
        {
            PublishWeight(weightKg, rawLine);
        }
    }

    private void PublishWeight(decimal weightKg, string rawLine)
    {
        _currentWeightKg = weightKg;
        WeightChanged?.Invoke(
            this,
            new WeightChangedEventArgs(weightKg, rawLine, DateTimeOffset.Now));
    }

    private void PublishConnection(bool isConnected, string status)
    {
        _isConnected = isConnected;
        _currentStatus = status;
        ConnectionChanged?.Invoke(
            this,
            new WeighbridgeConnectionChangedEventArgs(isConnected, status));
    }

    private void ClosePort()
    {
        var port = _port;
        _port = null;
        if (port is null)
        {
            return;
        }

        try { port.DataReceived -= OnDataReceived; } catch { }
        try { if (port.IsOpen) port.Close(); } catch { }
        try { port.Dispose(); } catch { }
    }

    private static void CopySettings(WeighbridgeOptions source, WeighbridgeOptions destination)
    {
        destination.Enabled = source.Enabled;
        destination.UniversalFormatEnabled = source.UniversalFormatEnabled;
        destination.TargetWeightKg = source.TargetWeightKg;
        destination.ResetWeightKg = source.ResetWeightKg;
        destination.StableReadCount = source.StableReadCount;
        destination.DataPrefix = source.DataPrefix;
        destination.UnitText = source.UnitText;
        destination.Serial = new SerialPortOptions
        {
            PortName = source.Serial.PortName,
            ReadMode = source.Serial.ReadMode,
            BaudRate = source.Serial.BaudRate,
            DataBits = source.Serial.DataBits,
            Parity = source.Serial.Parity,
            StopBits = source.Serial.StopBits
        };
    }

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var parity) ? parity : Parity.None;

    private static StopBits ParseStopBits(string value) =>
        Enum.TryParse<StopBits>(value, true, out var stopBits) ? stopBits : StopBits.One;

    private static string DecodeEscapes(string value) =>
        (value ?? "\\r\\n")
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClosePort();
        _lifecycleLock.Dispose();
        base.Dispose();
    }
}
