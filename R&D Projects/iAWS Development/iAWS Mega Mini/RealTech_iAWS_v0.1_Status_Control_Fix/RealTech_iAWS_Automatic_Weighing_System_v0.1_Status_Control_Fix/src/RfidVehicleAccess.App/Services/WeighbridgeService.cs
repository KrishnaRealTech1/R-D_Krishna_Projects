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
/// Expected input is line-oriented data such as: "wn009011 kg".
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

    [GeneratedRegex(@"^\s*(?<prefix>[A-Za-z]+)\s*(?<weight>[+-]?\d+(?:\.\d+)?)\s*(?<unit>[A-Za-z]+)?\s*$", RegexOptions.Compiled)]
    private static partial Regex WeightLineRegex();

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

        return decimal.TryParse(
            match.Groups["weight"].Value,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out weightKg) && weightKg >= 0m;
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
            _readBuffer.Append(text.Replace("\r\n", "\n").Replace('\r', '\n'));
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

            // Some indicators send short complete frames without CR/LF. Only accept
            // such a frame when the configured unit is already present at the end.
            // This prevents a partial serial chunk such as "wn009" from being
            // mistaken for a complete 9 kg reading before the rest of the frame arrives.
            if (_readBuffer.Length > 0 && _readBuffer.Length < 64)
            {
                var candidate = _readBuffer.ToString().Trim();
                var expectedUnit = _options.Weighbridge.UnitText?.Trim() ?? string.Empty;
                var looksComplete = !string.IsNullOrWhiteSpace(expectedUnit) &&
                                    candidate.EndsWith(expectedUnit, StringComparison.OrdinalIgnoreCase);

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
