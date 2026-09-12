using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class IawsHardwareGateway : IHostedService, IDisposable
{
    private readonly AppOptions _options;
    private readonly AppLogger _logger;
    private readonly UhfCfFrameDecoder _rfidDecoder = new();
    private readonly object _sync = new();
    private readonly CancellationTokenSource _shutdown = new();

    private SerialPort? _rfidPort;
    private SerialPort? _weightPort;
    private Task? _reconnectTask;
    private string _weightTextBuffer = string.Empty;
    private string _lastRfid = string.Empty;
    private DateTimeOffset _lastRfidAt = DateTimeOffset.MinValue;

    public IawsHardwareGateway(AppOptions options, AppLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public event EventHandler<RfidReadEventArgs>? RfidReceived;
    public event EventHandler<WeightChangedEventArgs>? WeightChanged;
    public event EventHandler<string>? ConnectionStatusChanged;

    public string CurrentStatus { get; private set; } = "Not started";
    public decimal CurrentWeightKg { get; private set; }

    public IReadOnlyList<string> GetAvailablePortNames() =>
        SerialPort.GetPortNames()
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.Hardware.SimulationEnabled)
        {
            PublishStatus("SIMULATION - RFID and weighbridge inputs are manual");
            return Task.CompletedTask;
        }

        OpenPorts();
        _reconnectTask = RunReconnectLoopAsync(_shutdown.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdown.Cancel();
        ClosePorts();
        if (_reconnectTask is not null)
        {
            try
            {
                await _reconnectTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch
            {
                // Shutdown must not be blocked by a reconnect loop.
            }
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        ClosePorts();
        _rfidDecoder.Reset();
        _weightTextBuffer = string.Empty;

        if (_options.Hardware.SimulationEnabled)
        {
            PublishStatus("SIMULATION - RFID and weighbridge inputs are manual");
            return;
        }

        await Task.Delay(100, cancellationToken);
        OpenPorts();
        if (_reconnectTask is null || _reconnectTask.IsCompleted)
        {
            _reconnectTask = RunReconnectLoopAsync(_shutdown.Token);
        }
    }

    public void SimulateRfid(string rfid)
    {
        if (string.IsNullOrWhiteSpace(rfid))
        {
            return;
        }

        PublishRfid(rfid.Trim());
    }

    public void SimulateWeight(decimal weightKg) =>
        PublishWeight(Math.Max(0m, weightKg));

    private void OpenPorts()
    {
        lock (_sync)
        {
            ClosePortsUnsafe();

            try
            {
                _rfidPort = CreateSerialPort(_options.Hardware.RfidReader);
                _rfidPort.DataReceived += OnRfidDataReceived;
                _rfidPort.Open();
            }
            catch (Exception ex)
            {
                DisposePort(ref _rfidPort, OnRfidDataReceived);
                _ = _logger.StatusAsync($"RFID reader connection failed: {ex.Message}");
            }

            try
            {
                _weightPort = CreateWeightPort(_options.Hardware.WeightBridge);
                _weightPort.DataReceived += OnWeightDataReceived;
                _weightPort.Open();
            }
            catch (Exception ex)
            {
                DisposePort(ref _weightPort, OnWeightDataReceived);
                _ = _logger.StatusAsync($"Weighbridge connection failed: {ex.Message}");
            }

            PublishConnectionSummary();
        }
    }

    private async Task RunReconnectLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(
                TimeSpan.FromSeconds(Math.Max(2, _options.Hardware.ReconnectSeconds)));

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_options.Hardware.SimulationEnabled)
                {
                    continue;
                }

                if (_rfidPort?.IsOpen == true && _weightPort?.IsOpen == true)
                {
                    continue;
                }

                OpenPorts();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }

    private void OnRfidDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        var port = _rfidPort;
        if (port is null || !port.IsOpen)
        {
            return;
        }

        try
        {
            if (string.Equals(
                    _options.Hardware.RfidReader.ReadMode,
                    "UhfCfFrame",
                    StringComparison.OrdinalIgnoreCase))
            {
                var available = port.BytesToRead;
                if (available <= 0)
                {
                    return;
                }

                var buffer = new byte[available];
                var read = port.Read(buffer, 0, buffer.Length);
                foreach (var epc in _rfidDecoder.Append(buffer.AsSpan(0, read)))
                {
                    PublishRfid(epc);
                }
            }
            else
            {
                var line = port.ReadLine().Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    PublishRfid(line);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            _ = _logger.StatusAsync($"RFID read error: {ex.Message}");
        }
    }

    private void OnWeightDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        var port = _weightPort;
        if (port is null || !port.IsOpen)
        {
            return;
        }

        try
        {
            var chunk = port.ReadExisting();
            if (string.IsNullOrEmpty(chunk))
            {
                return;
            }

            _weightTextBuffer += chunk;
            var normalized = _weightTextBuffer.Replace('\r', '\n');
            var lines = normalized.Split('\n');
            _weightTextBuffer = lines[^1];

            foreach (var line in lines[..^1])
            {
                TryPublishWeight(line);
            }

            // The deployed weighbridge indicator can stream frames such as:
            //   wn000000 kg
            //   wn009011 kg
            // Some devices include CR/LF and some stream the same frame without a
            // reliable line ending. Consume every complete "wn...kg" frame that is
            // already present in the remaining buffer so the UI updates immediately.
            var frameMatches = Regex.Matches(
                _weightTextBuffer,
                @"(?i)\bwn\s*(?<weight>\d{1,9}(?:\.\d+)?)\s*kg\b",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));

            if (frameMatches.Count > 0)
            {
                foreach (Match frame in frameMatches)
                {
                    TryPublishWeight(frame.Value);
                }

                var last = frameMatches[frameMatches.Count - 1];
                _weightTextBuffer = _weightTextBuffer[(last.Index + last.Length)..];
            }
            else if (_weightTextBuffer.Length > 64)
            {
                // Safety fallback for other indicators that provide a long token
                // without line endings. Keep this bounded to prevent buffer growth.
                TryPublishWeight(_weightTextBuffer);
                _weightTextBuffer = string.Empty;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            _ = _logger.StatusAsync($"Weighbridge read error: {ex.Message}");
        }
    }

    private void TryPublishWeight(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        Match match;
        try
        {
            match = Regex.Match(
                raw,
                string.IsNullOrWhiteSpace(_options.Hardware.WeightBridge.WeightPattern)
                    ? @"[-+]?\d+(?:\.\d+)?"
                    : _options.Hardware.WeightBridge.WeightPattern,
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            match = Regex.Match(raw, @"[-+]?\d+(?:\.\d+)?", RegexOptions.CultureInvariant);
        }

        var numericText = match.Success && match.Groups.Count > 1 && match.Groups[1].Success
            ? match.Groups[1].Value
            : match.Value;

        if (!match.Success ||
            !decimal.TryParse(
                numericText,
                NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var weight))
        {
            return;
        }

        PublishWeight(Math.Max(0m, weight));
    }

    private void PublishRfid(string rfid)
    {
        var normalized = rfid.Trim();
        if (normalized.Length == 0)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        if (string.Equals(normalized, _lastRfid, StringComparison.OrdinalIgnoreCase) &&
            now - _lastRfidAt < TimeSpan.FromSeconds(2))
        {
            return;
        }

        _lastRfid = normalized;
        _lastRfidAt = now;
        RfidReceived?.Invoke(this, new RfidReadEventArgs(normalized, now));
    }

    private void PublishWeight(decimal weightKg)
    {
        CurrentWeightKg = weightKg;
        WeightChanged?.Invoke(this, new WeightChangedEventArgs(weightKg, DateTimeOffset.Now));
    }

    private void PublishConnectionSummary()
    {
        var rfid = _rfidPort?.IsOpen == true ? $"RFID {_rfidPort.PortName}" : "RFID offline";
        var weight = _weightPort?.IsOpen == true ? $"Weight {_weightPort.PortName}" : "Weight offline";
        PublishStatus($"{rfid} | {weight}");
    }

    private void PublishStatus(string status)
    {
        CurrentStatus = status;
        ConnectionStatusChanged?.Invoke(this, status);
    }

    private static SerialPort CreateSerialPort(SerialPortOptions options) =>
        new(
            options.PortName,
            options.BaudRate,
            ParseParity(options.Parity),
            options.DataBits,
            ParseStopBits(options.StopBits))
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            NewLine = "\r\n"
        };

    private static SerialPort CreateWeightPort(WeightBridgeOptions options) =>
        new(
            options.PortName,
            options.BaudRate,
            ParseParity(options.Parity),
            options.DataBits,
            ParseStopBits(options.StopBits))
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            NewLine = "\r\n",
            Encoding = Encoding.ASCII
        };

    private static Parity ParseParity(string value) =>
        Enum.TryParse<Parity>(value, true, out var parity) ? parity : Parity.None;

    private static StopBits ParseStopBits(string value) =>
        Enum.TryParse<StopBits>(value, true, out var stopBits) ? stopBits : StopBits.One;

    private void ClosePorts()
    {
        lock (_sync)
        {
            ClosePortsUnsafe();
        }
    }

    private void ClosePortsUnsafe()
    {
        DisposePort(ref _rfidPort, OnRfidDataReceived);
        DisposePort(ref _weightPort, OnWeightDataReceived);
    }

    private static void DisposePort(
        ref SerialPort? port,
        SerialDataReceivedEventHandler handler)
    {
        var current = port;
        port = null;
        if (current is null)
        {
            return;
        }

        try
        {
            current.DataReceived -= handler;
            if (current.IsOpen)
            {
                current.Close();
            }
        }
        catch
        {
            // Best effort cleanup.
        }
        finally
        {
            current.Dispose();
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        ClosePorts();
        _shutdown.Dispose();
    }
}
