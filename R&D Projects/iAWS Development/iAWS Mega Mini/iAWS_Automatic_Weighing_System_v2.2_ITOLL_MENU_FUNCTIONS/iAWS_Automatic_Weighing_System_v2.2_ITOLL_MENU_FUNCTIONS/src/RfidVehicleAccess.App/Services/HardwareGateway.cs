using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class HardwareConnectionStatusChangedEventArgs : EventArgs
{
    public HardwareConnectionStatusChangedEventArgs(bool isReady, string status)
    {
        IsReady = isReady;
        Status = status;
    }

    public bool IsReady { get; }
    public string Status { get; }
}


public sealed class VehicleSensorStateChangedEventArgs : EventArgs
{
    public VehicleSensorStateChangedEventArgs(LaneDirection direction, bool isHigh)
    {
        Direction = direction;
        IsHigh = isHigh;
    }

    public LaneDirection Direction { get; }
    public bool IsHigh { get; }
}

public sealed class HardwareApplyResult
{
    public HardwareApplyResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public bool Success { get; }
    public string Message { get; }
}

public sealed class HardwareGateway : IHostedService, IDisposable
{
    private readonly AppOptions _options;
    private readonly AppLogger _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _controlWriteLock = new(1, 1);
    private readonly UhfCfFrameDecoder _inRfidDecoder = new();
    private readonly UhfCfFrameDecoder _outRfidDecoder = new();
    private readonly object _rfidPublishSync = new();
    private readonly Dictionary<LaneDirection, (string Epc, long Tick)> _lastPublishedRfids = [];
    private readonly object _controlReadSync = new();
    private readonly StringBuilder _controlReadBuffer = new();

    private SerialPort? _inRfidPort;
    private SerialPort? _outRfidPort;
    private SerialPort? _controlPort;
    private SerialPort? _bluetoothControlPort;
    private TcpClient? _wifiControlClient;
    private NetworkStream? _wifiControlStream;
    private CancellationTokenSource? _wifiReadCts;
    private Task? _wifiReadTask;
    private string _activeControlTransport = "None";
    private long _lastControlWriteTick;
    private CancellationTokenSource? _reconnectCts;
    private Task? _reconnectTask;
    private bool _started;
    private bool _disposed;
    private string _currentStatus = "Hardware starting...";
    private bool _isReady;

    public HardwareGateway(AppOptions options, AppLogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public event EventHandler<(LaneDirection Direction, string Rfid)>? RfidReceived;
    public event EventHandler<VehicleSensorStateChangedEventArgs>? VehicleSensorStateChanged;
    public event EventHandler<string>? ControlCommandSent;
    public event EventHandler<string>? ControlTraffic;
    public event EventHandler<HardwareConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

    public string CurrentStatus => _currentStatus;
    public bool IsReady => _isReady;
    public string ActiveControlTransport => _activeControlTransport;

    public string[] GetAvailablePortNames() => SerialPort
        .GetPortNames()
        .OrderBy(GetPortSortNumber)
        .ThenBy(port => port, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            _started = true;
            _reconnectCts = new CancellationTokenSource();
            await ConnectConfiguredPortsAsync(cancellationToken);
            _reconnectTask = ReconnectLoopAsync(_reconnectCts.Token);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? reconnectCts;
        Task? reconnectTask;

        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            _started = false;
            reconnectCts = _reconnectCts;
            reconnectTask = _reconnectTask;
            _reconnectCts = null;
            _reconnectTask = null;

            reconnectCts?.Cancel();
            CloseAndDisposePorts();
            PublishStatus(false, "Hardware stopped");
        }
        finally
        {
            _lifecycleLock.Release();
        }

        if (reconnectTask is not null)
        {
            try
            {
                await reconnectTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during application shutdown.
            }
            catch (TimeoutException)
            {
                // Shutdown must continue even if a serial driver is delayed.
            }
        }

        reconnectCts?.Dispose();
        await _logger.StatusAsync("Hardware connections stopped.", cancellationToken);
    }

    public async Task<HardwareApplyResult> ApplySettingsAsync(
        HardwareOptions settings,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            CopyHardwareOptions(settings, _options.Hardware);
            CloseAndDisposePorts();

            if (!_started)
            {
                const string message = "COM settings saved. They will be used when hardware starts.";
                PublishStatus(false, message);
                return new HardwareApplyResult(true, message);
            }

            var connected = await ConnectConfiguredPortsAsync(cancellationToken);
            return connected
                ? new HardwareApplyResult(true, _currentStatus)
                : new HardwareApplyResult(false, _currentStatus);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task SetLaneIdleStateAsync(
        LaneDirection direction,
        CancellationToken cancellationToken = default)
    {
        var lane = direction == LaneDirection.In ? "IN" : "OUT";
        await SendControlCommandAsync($"CLOSE {lane} BB", cancellationToken);
        // The controller exposes BUZZER OFF as a dedicated command. Do not rely on
        // ALL OFF to clear a latched buzzer output.
        await SendControlCommandAsync($"{lane} Buzzer OFF", cancellationToken);
        await SendControlCommandAsync($"{lane} ALL OFF", cancellationToken);
        await SendControlCommandAsync($"{lane} GRN", cancellationToken);
    }

    private async Task SetAllLanesIdleAsync(
        CancellationToken cancellationToken)
    {
        await SetLaneIdleStateAsync(LaneDirection.In, cancellationToken);
        await SetLaneIdleStateAsync(LaneDirection.Out, cancellationToken);
        await _logger.StatusAsync(
            "Idle outputs applied: IN/OUT barriers closed and signals green.",
            cancellationToken);
    }

    public async Task SendControlCommandAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        command = command.Trim();

        await _controlWriteLock.WaitAsync(cancellationToken);
        try
        {
            if (_options.Hardware.SimulationEnabled)
            {
                await _logger.StatusAsync($"SIMULATED command sent: {command}", cancellationToken);
                PublishControlTraffic($"[TX][SIM] {command}");
                ControlCommandSent?.Invoke(this, command);
                return;
            }

            if (!IsActiveControlLinkReady())
                await ActivateBestControlLinkAsync(cancellationToken);

            if (!IsActiveControlLinkReady())
            {
                await _logger.StatusAsync(
                    $"IND command not sent because wired, Bluetooth and Wi-Fi control links are unavailable: {command}",
                    cancellationToken);
                PublishControlTraffic($"[ERR][NO LINK] {command}");
                PublishConnectionSummary();
                return;
            }

            try
            {
                await WriteActiveControlAsync(command, cancellationToken);
            }
            catch (Exception firstError)
            {
                var failedTransport = _activeControlTransport;
                PublishControlTraffic($"[ERR][{failedTransport}] {firstError.Message}");
                await _logger.StatusAsync(
                    $"IND {failedTransport} command failed; selecting next failover link: {firstError.Message}",
                    cancellationToken);
                CloseActiveControlLink();
                await ActivateBestControlLinkAsync(cancellationToken);
                if (!IsActiveControlLinkReady())
                {
                    PublishConnectionSummary();
                    return;
                }
                await WriteActiveControlAsync(command, cancellationToken);
            }

            _lastControlWriteTick = Environment.TickCount64;
            var transport = _activeControlTransport;
            await _logger.StatusAsync($"Command sent via {transport}: {command}", cancellationToken);
            PublishControlTraffic($"[TX][{transport}] {command}");
            ControlCommandSent?.Invoke(this, command);
            PublishConnectionSummary();
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync($"IND control command failed ({command}): {ex.Message}", cancellationToken);
            PublishControlTraffic($"[ERR][{_activeControlTransport}] {ex.Message}");
            CloseActiveControlLink();
            PublishConnectionSummary();
        }
        finally
        {
            _controlWriteLock.Release();
        }
    }

    public Task SendTerminalCommandAsync(
        string command,
        CancellationToken cancellationToken = default) =>
        SendControlCommandAsync(command, cancellationToken);

    public void SimulateSensor(LaneDirection direction, bool isHigh)
    {
        PublishSensorState(direction, isHigh);
    }

    public void SimulateRfid(LaneDirection direction, string rfid)
    {
        RfidReceived?.Invoke(this, (direction, rfid));
    }

    private async Task<bool> ConnectConfiguredPortsAsync(CancellationToken cancellationToken)
    {
        if (_options.Hardware.SimulationEnabled)
        {
            const string status = "Simulation mode - COM ports disabled";
            _activeControlTransport = "Simulation";
            PublishStatus(true, status);
            await _logger.StatusAsync(status, cancellationToken);
            await SetAllLanesIdleAsync(cancellationToken);
            return true;
        }

        try
        {
            ValidatePortSettings(_options.Hardware);

            OpenRfidPorts();
            await ActivateBestControlLinkAsync(cancellationToken);
            PublishConnectionSummary();

            if (!IsAllRequiredHardwareReady())
            {
                await _logger.StatusAsync(_currentStatus, cancellationToken);
                return false;
            }

            await _logger.StatusAsync($"Hardware connected: {_currentStatus}.", cancellationToken);
            await SetAllLanesIdleAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            CloseAndDisposePorts();
            var status = $"Hardware connection failed: {ex.Message}";
            PublishStatus(false, status);
            await _logger.StatusAsync(status, cancellationToken);
            PublishControlTraffic($"[ERR][CONNECT] {ex.Message}");
            return false;
        }
    }

    private void OpenRfidPorts()
    {
        _inRfidPort = CreatePort(_options.Hardware.InRfid);
        _outRfidPort = CreatePort(_options.Hardware.OutRfid);
        _inRfidPort.DataReceived += OnInRfidDataReceived;
        _outRfidPort.DataReceived += OnOutRfidDataReceived;
        _inRfidPort.Open();
        _outRfidPort.Open();
    }

    private async Task ActivateBestControlLinkAsync(CancellationToken cancellationToken)
    {
        if (_controlPort?.IsOpen == true)
        {
            _activeControlTransport = $"Wired {_controlPort.PortName}";
            return;
        }

        if (TryOpenPrimaryControl(out var primaryError))
        {
            await _logger.StatusAsync($"IND primary control connected on {_controlPort!.PortName}.", cancellationToken);
            return;
        }
        PublishControlTraffic($"[WARN][WIRED] {primaryError}");

        if (TryOpenBluetoothControl(out var bluetoothError))
        {
            await _logger.StatusAsync($"IND wired unavailable; Bluetooth failover active on {_bluetoothControlPort!.PortName}.", cancellationToken);
            return;
        }
        PublishControlTraffic($"[WARN][BLUETOOTH] {bluetoothError}");

        if (await TryOpenWifiControlAsync(cancellationToken))
        {
            await _logger.StatusAsync($"IND wired/Bluetooth unavailable; Wi-Fi failover active on {_options.Hardware.WifiControl.Host}:{_options.Hardware.WifiControl.Port}.", cancellationToken);
            return;
        }

        _activeControlTransport = "None";
    }

    private bool TryOpenPrimaryControl(out string error)
    {
        error = string.Empty;
        if (_controlPort?.IsOpen == true)
        {
            _activeControlTransport = $"Wired {_controlPort.PortName}";
            return true;
        }

        CloseAndDisposePort(ref _controlPort, OnControlDataReceived);
        try
        {
            _controlPort = CreatePort(_options.Hardware.Control);
            _controlPort.DataReceived += OnControlDataReceived;
            _controlPort.Open();
            _activeControlTransport = $"Wired {_controlPort.PortName}";
            PublishControlTraffic($"[LINK] Wired control connected: {_controlPort.PortName}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            CloseAndDisposePort(ref _controlPort, OnControlDataReceived);
            return false;
        }
    }

    private bool TryOpenBluetoothControl(out string error)
    {
        error = string.Empty;
        if (!_options.Hardware.BluetoothControlEnabled)
        {
            error = "Bluetooth backup is disabled in COM Port Settings.";
            return false;
        }

        if (_bluetoothControlPort?.IsOpen == true)
        {
            _activeControlTransport = $"Bluetooth {_bluetoothControlPort.PortName}";
            return true;
        }

        CloseAndDisposePort(ref _bluetoothControlPort, OnBluetoothControlDataReceived);
        try
        {
            _bluetoothControlPort = CreatePort(_options.Hardware.BluetoothControl);
            _bluetoothControlPort.DataReceived += OnBluetoothControlDataReceived;
            _bluetoothControlPort.Open();
            _activeControlTransport = $"Bluetooth {_bluetoothControlPort.PortName}";
            PublishControlTraffic($"[LINK] Bluetooth failover connected: {_bluetoothControlPort.PortName}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            CloseAndDisposePort(ref _bluetoothControlPort, OnBluetoothControlDataReceived);
            return false;
        }
    }

    private async Task<bool> TryOpenWifiControlAsync(CancellationToken cancellationToken)
    {
        if (!_options.Hardware.WifiControlEnabled)
        {
            PublishControlTraffic("[WARN][WIFI] Wi-Fi backup is disabled.");
            return false;
        }
        if (_wifiControlClient?.Connected == true && _wifiControlStream is not null)
        {
            _activeControlTransport = $"WiFi {_options.Hardware.WifiControl.Host}:{_options.Hardware.WifiControl.Port}";
            return true;
        }

        CloseWifiControl();
        try
        {
            var client = new TcpClient();
            var timeout = Math.Max(250, _options.Hardware.WifiControl.ConnectTimeoutMilliseconds);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            await client.ConnectAsync(_options.Hardware.WifiControl.Host.Trim(), _options.Hardware.WifiControl.Port, timeoutCts.Token);
            _wifiControlClient = client;
            _wifiControlStream = client.GetStream();
            _wifiReadCts = CancellationTokenSource.CreateLinkedTokenSource(_reconnectCts?.Token ?? CancellationToken.None);
            _wifiReadTask = WifiReadLoopAsync(_wifiReadCts.Token);
            _activeControlTransport = $"WiFi {_options.Hardware.WifiControl.Host}:{_options.Hardware.WifiControl.Port}";
            PublishControlTraffic($"[LINK] Wi-Fi failover connected: {_options.Hardware.WifiControl.Host}:{_options.Hardware.WifiControl.Port}");
            return true;
        }
        catch (Exception ex)
        {
            PublishControlTraffic($"[ERR][WIFI] {ex.Message}");
            CloseWifiControl();
            return false;
        }
    }

    private async Task WifiReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[2048];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _wifiControlStream is not null)
            {
                var read = await _wifiControlStream.ReadAsync(buffer, cancellationToken);
                if (read <= 0) break;
                ProcessControlText(Encoding.ASCII.GetString(buffer, 0, read), $"WiFi {_options.Hardware.WifiControl.Host}:{_options.Hardware.WifiControl.Port}", true);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { PublishControlTraffic($"[ERR][WIFI RX] {ex.Message}"); }
        finally
        {
            if (_activeControlTransport.StartsWith("WiFi", StringComparison.OrdinalIgnoreCase))
                _activeControlTransport = "None";
        }
    }

    private async Task WriteActiveControlAsync(string command, CancellationToken cancellationToken)
    {
        if (_activeControlTransport.StartsWith("Wired", StringComparison.OrdinalIgnoreCase) && _controlPort?.IsOpen == true)
        {
            _controlPort.WriteLine(command);
            return;
        }
        if (_activeControlTransport.StartsWith("Bluetooth", StringComparison.OrdinalIgnoreCase) && _bluetoothControlPort?.IsOpen == true)
        {
            _bluetoothControlPort.WriteLine(command);
            return;
        }
        if (_activeControlTransport.StartsWith("WiFi", StringComparison.OrdinalIgnoreCase) && _wifiControlStream is not null)
        {
            var bytes = Encoding.ASCII.GetBytes(command + DecodeEscapes(_options.Hardware.LineTerminator));
            await _wifiControlStream.WriteAsync(bytes, cancellationToken);
            await _wifiControlStream.FlushAsync(cancellationToken);
            return;
        }
        throw new IOException("No active IND control link.");
    }

    private bool IsActiveControlLinkReady()
    {
        if (_activeControlTransport.StartsWith("Wired", StringComparison.OrdinalIgnoreCase)) return _controlPort?.IsOpen == true;
        if (_activeControlTransport.StartsWith("Bluetooth", StringComparison.OrdinalIgnoreCase)) return _bluetoothControlPort?.IsOpen == true;
        if (_activeControlTransport.StartsWith("WiFi", StringComparison.OrdinalIgnoreCase)) return _wifiControlClient?.Connected == true && _wifiControlStream is not null;
        return false;
    }

    private void CloseActiveControlLink()
    {
        if (_activeControlTransport.StartsWith("Wired", StringComparison.OrdinalIgnoreCase)) CloseAndDisposePort(ref _controlPort, OnControlDataReceived);
        else if (_activeControlTransport.StartsWith("Bluetooth", StringComparison.OrdinalIgnoreCase)) CloseAndDisposePort(ref _bluetoothControlPort, OnBluetoothControlDataReceived);
        else if (_activeControlTransport.StartsWith("WiFi", StringComparison.OrdinalIgnoreCase)) CloseWifiControl();
        _activeControlTransport = "None";
    }

    private void CloseWifiControl()
    {
        try { _wifiReadCts?.Cancel(); } catch { }
        try { _wifiControlStream?.Dispose(); } catch { }
        try { _wifiControlClient?.Dispose(); } catch { }
        _wifiReadCts?.Dispose();
        _wifiReadCts = null;
        _wifiReadTask = null;
        _wifiControlStream = null;
        _wifiControlClient = null;
    }

    private async Task ReconnectLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var delaySeconds = Math.Max(2, _options.Hardware.ReconnectSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_started || _options.Hardware.SimulationEnabled)
            {
                continue;
            }

            try
            {
                await _lifecycleLock.WaitAsync(cancellationToken);
                try
                {
                    if (!_started || _options.Hardware.SimulationEnabled)
                    {
                        continue;
                    }

                    if (_inRfidPort?.IsOpen != true || _outRfidPort?.IsOpen != true)
                    {
                        CloseAndDisposePort(ref _inRfidPort, OnInRfidDataReceived);
                        CloseAndDisposePort(ref _outRfidPort, OnOutRfidDataReceived);
                        try
                        {
                            OpenRfidPorts();
                        }
                        catch (Exception ex)
                        {
                            await _logger.StatusAsync($"RFID reconnect failed: {ex.Message}", cancellationToken);
                        }
                    }

                    // Maintain strict priority: Wired COM -> Bluetooth COM -> Wi-Fi TCP.
                    // Fail back only during a short command-idle window.
                    var idleMilliseconds = Environment.TickCount64 - _lastControlWriteTick;
                    var canFailBack = idleMilliseconds >= 2500;

                    if (canFailBack && _controlPort?.IsOpen != true)
                    {
                        if (TryOpenPrimaryControl(out var primaryError))
                        {
                            CloseAndDisposePort(ref _bluetoothControlPort, OnBluetoothControlDataReceived);
                            CloseWifiControl();
                            await _logger.StatusAsync($"IND control restored to primary wired port {_controlPort!.PortName}.", cancellationToken);
                        }
                        else
                        {
                            PublishControlTraffic($"[WARN][WIRED RETRY] {primaryError}");
                            if (_bluetoothControlPort?.IsOpen != true)
                            {
                                if (TryOpenBluetoothControl(out var bluetoothError))
                                {
                                    CloseWifiControl();
                                    await _logger.StatusAsync($"IND control restored to Bluetooth {_bluetoothControlPort!.PortName}.", cancellationToken);
                                }
                                else
                                {
                                    PublishControlTraffic($"[WARN][BLUETOOTH RETRY] {bluetoothError}");
                                    if (!IsActiveControlLinkReady() || !_activeControlTransport.StartsWith("WiFi", StringComparison.OrdinalIgnoreCase))
                                        await TryOpenWifiControlAsync(cancellationToken);
                                }
                            }
                        }
                    }
                    else if (!IsActiveControlLinkReady())
                    {
                        await ActivateBestControlLinkAsync(cancellationToken);
                    }

                    PublishConnectionSummary();
                }
                finally
                {
                    _lifecycleLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private SerialPort? GetActiveControlPort()
    {
        if (_activeControlTransport.StartsWith("Wired", StringComparison.OrdinalIgnoreCase) &&
            _controlPort?.IsOpen == true)
        {
            return _controlPort;
        }

        if (_activeControlTransport.StartsWith("Bluetooth", StringComparison.OrdinalIgnoreCase) &&
            _bluetoothControlPort?.IsOpen == true)
        {
            return _bluetoothControlPort;
        }

        if (_controlPort?.IsOpen == true)
        {
            _activeControlTransport = $"Wired {_controlPort.PortName}";
            return _controlPort;
        }

        if (_bluetoothControlPort?.IsOpen == true)
        {
            _activeControlTransport = $"Bluetooth {_bluetoothControlPort.PortName}";
            return _bluetoothControlPort;
        }

        return null;
    }

    private SerialPort CreatePort(SerialPortOptions settings)
    {
        var port = new SerialPort(
            settings.PortName.Trim(),
            settings.BaudRate,
            Enum.Parse<Parity>(settings.Parity, true),
            settings.DataBits,
            Enum.Parse<StopBits>(settings.StopBits, true))
        {
            NewLine = DecodeEscapes(_options.Hardware.LineTerminator),
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            DtrEnable = false,
            RtsEnable = false
        };

        return port;
    }

    private void OnInRfidDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (sender is SerialPort port)
        {
            ReadRfid(
                port,
                LaneDirection.In,
                _options.Hardware.InRfid,
                _inRfidDecoder);
        }
    }

    private void OnOutRfidDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (sender is SerialPort port)
        {
            ReadRfid(
                port,
                LaneDirection.Out,
                _options.Hardware.OutRfid,
                _outRfidDecoder);
        }
    }

    private void OnControlDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (sender is SerialPort port)
        {
            ReadControlMessage(port, $"Wired {port.PortName}");
        }
    }

    private void OnBluetoothControlDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (sender is SerialPort port)
        {
            ReadControlMessage(port, $"Bluetooth {port.PortName}");
        }
    }

    private void ReadRfid(
        SerialPort port,
        LaneDirection direction,
        SerialPortOptions settings,
        UhfCfFrameDecoder binaryDecoder)
    {
        try
        {
            if (string.Equals(
                    settings.ReadMode,
                    "UhfCfFrame",
                    StringComparison.OrdinalIgnoreCase))
            {
                ReadBinaryRfid(port, direction, binaryDecoder);
                return;
            }

            ReadLineRfid(port, direction);
        }
        catch (TimeoutException)
        {
            // A partial line or packet will be completed by a later DataReceived event.
        }
        catch (Exception ex)
        {
            _ = _logger.StatusAsync(
                $"{direction.ToString().ToUpperInvariant()} RFID read error: {ex.Message}");
            PublishStatus(false, $"{direction.ToString().ToUpperInvariant()} RFID COM error");
        }
    }

    private void ReadBinaryRfid(
        SerialPort port,
        LaneDirection direction,
        UhfCfFrameDecoder decoder)
    {
        while (port.BytesToRead > 0)
        {
            var bytesAvailable = port.BytesToRead;
            var buffer = new byte[Math.Min(bytesAvailable, 4096)];
            var bytesRead = port.Read(buffer, 0, buffer.Length);

            if (bytesRead <= 0)
            {
                return;
            }

            foreach (var epc in decoder.Append(buffer.AsSpan(0, bytesRead)))
            {
                PublishRfid(direction, epc);
            }
        }
    }

    private void ReadLineRfid(SerialPort port, LaneDirection direction)
    {
        while (port.BytesToRead > 0)
        {
            var line = port.ReadLine().Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                PublishRfid(direction, line);
            }
        }
    }

    private void PublishRfid(LaneDirection direction, string rfid)
    {
        const long repeatSuppressionMilliseconds = 750;
        var now = Environment.TickCount64;

        lock (_rfidPublishSync)
        {
            if (_lastPublishedRfids.TryGetValue(direction, out var previous) &&
                previous.Epc.Equals(rfid, StringComparison.OrdinalIgnoreCase) &&
                now - previous.Tick < repeatSuppressionMilliseconds)
            {
                return;
            }

            _lastPublishedRfids[direction] = (rfid, now);
        }

        RfidReceived?.Invoke(this, (direction, rfid));
    }

    private void ProcessControlText(string incoming, string source, bool isActiveSource)
    {
        List<string> completeLines;
        lock (_controlReadSync)
        {
            if (string.IsNullOrEmpty(incoming)) return;
            _controlReadBuffer.Append(incoming);
            completeLines = ExtractCompleteControlLines();
        }
        foreach (var line in completeLines)
        {
            PublishControlTraffic($"[RX][{source}] {line}");
            if (!isActiveSource) continue;
            if (TryParseSensorState(line, out var direction, out var isHigh))
            {
                _ = _logger.StatusAsync($"Control-unit message received via {source}: {line} ({direction}, {(isHigh ? "Detected" : "Realeased")}).");
                PublishSensorState(direction, isHigh);
            }
        }
    }

    private void ReadControlMessage(SerialPort port, string source)
    {
        try
        {
            var incoming = port.ReadExisting();
            var active = (_activeControlTransport.StartsWith("Wired", StringComparison.OrdinalIgnoreCase) && ReferenceEquals(port, _controlPort)) ||
                         (_activeControlTransport.StartsWith("Bluetooth", StringComparison.OrdinalIgnoreCase) && ReferenceEquals(port, _bluetoothControlPort));
            ProcessControlText(incoming, source, active);
        }
        catch (Exception ex)
        {
            _ = _logger.StatusAsync($"Control read error via {source}: {ex.Message}");
            PublishControlTraffic($"[ERR][RX][{source}] {ex.Message}");
        }
    }

    private List<string> ExtractCompleteControlLines()
    {
        var lines = new List<string>();

        while (true)
        {
            var delimiterIndex = -1;
            for (var index = 0; index < _controlReadBuffer.Length; index++)
            {
                var character = _controlReadBuffer[index];
                if (character is '\r' or '\n')
                {
                    delimiterIndex = index;
                    break;
                }
            }

            if (delimiterIndex < 0)
            {
                break;
            }

            var line = _controlReadBuffer.ToString(0, delimiterIndex).Trim();
            var removeCount = delimiterIndex + 1;
            while (removeCount < _controlReadBuffer.Length &&
                   _controlReadBuffer[removeCount] is '\r' or '\n')
            {
                removeCount++;
            }

            _controlReadBuffer.Remove(0, removeCount);
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    private bool TryParseSensorState(
        string message,
        out LaneDirection direction,
        out bool isHigh)
    {
        var normalized = NormalizeControlMessage(message);
        var configured = _options.Hardware.SensorMessages;

        // A lane completion is triggered only by the UNO release command. Raw LOW,
        // CLEAR, OFF, or NOT DETECTED messages are intentionally ignored so a moving
        // vehicle cannot close the barrier before the UNO confirms vehicle presence.
        // Accept both the requested UNO spelling ("Realeased") and the normal spelling.
        if (MatchesSensorMessage(normalized, configured.InReleased,
                "IN REALEASED", "IN REALESED", "IN RELEASED", "IN RELEASE"))
        {
            direction = LaneDirection.In;
            isHigh = false;
            return true;
        }

        if (MatchesSensorMessage(normalized, configured.OutReleased,
                "OUT REALEASED", "OUT REALESED", "OUT RELEASED", "OUT RELEASE"))
        {
            direction = LaneDirection.Out;
            isHigh = false;
            return true;
        }

        if (MatchesSensorMessage(normalized, configured.InHigh,
                "IN DETECTED", "IN HIGH", "IN SENSOR HIGH", "SENSOR IN HIGH", "IN ON"))
        {
            direction = LaneDirection.In;
            isHigh = true;
            return true;
        }

        if (MatchesSensorMessage(normalized, configured.OutHigh,
                "OUT DETECTED", "OUT HIGH", "OUT SENSOR HIGH", "SENSOR OUT HIGH", "OUT ON"))
        {
            direction = LaneDirection.Out;
            isHigh = true;
            return true;
        }

        direction = default;
        isHigh = false;
        return false;
    }

    private void PublishSensorState(LaneDirection direction, bool isHigh)
    {
        VehicleSensorStateChanged?.Invoke(
            this,
            new VehicleSensorStateChangedEventArgs(direction, isHigh));
    }

    private static bool MatchesSensorMessage(
        string normalizedMessage,
        string configuredMessage,
        params string[] aliases)
    {
        if (!string.IsNullOrWhiteSpace(configuredMessage) &&
            IsSensorMessageMatch(
                normalizedMessage,
                NormalizeControlMessage(configuredMessage)))
        {
            return true;
        }

        return aliases.Any(alias =>
            IsSensorMessageMatch(normalizedMessage, NormalizeControlMessage(alias)));
    }

    private static bool IsSensorMessageMatch(
        string normalizedMessage,
        string normalizedCandidate)
    {
        if (string.IsNullOrWhiteSpace(normalizedCandidate))
        {
            return false;
        }

        return normalizedMessage.Equals(normalizedCandidate, StringComparison.Ordinal) ||
               normalizedMessage.StartsWith(normalizedCandidate + " ", StringComparison.Ordinal) ||
               normalizedMessage.EndsWith(" " + normalizedCandidate, StringComparison.Ordinal) ||
               normalizedMessage.Contains(" " + normalizedCandidate + " ", StringComparison.Ordinal);
    }

    private static string NormalizeControlMessage(string message)
    {
        var normalizedCharacters = message
            .Trim()
            .ToUpperInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();

        return string.Join(' ', new string(normalizedCharacters)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private bool IsAllRequiredHardwareReady() =>
        _inRfidPort?.IsOpen == true &&
        _outRfidPort?.IsOpen == true &&
        IsActiveControlLinkReady();

    private void PublishConnectionSummary()
    {
        var inStatus = _inRfidPort?.IsOpen == true
            ? _inRfidPort.PortName
            : "DOWN";
        var outStatus = _outRfidPort?.IsOpen == true
            ? _outRfidPort.PortName
            : "DOWN";
        var controlStatus = IsActiveControlLinkReady()
            ? _activeControlTransport
            : "DOWN";
        PublishStatus(
            IsAllRequiredHardwareReady(),
            $"IN {inStatus} | OUT {outStatus} | IND {controlStatus}");
    }

    private void PublishControlTraffic(string message)
    {
        ControlTraffic?.Invoke(this, $"{DateTime.Now:HH:mm:ss.fff} {message}");
    }

    private void CloseAndDisposePorts()
    {
        CloseAndDisposePort(ref _inRfidPort, OnInRfidDataReceived);
        CloseAndDisposePort(ref _outRfidPort, OnOutRfidDataReceived);
        CloseAndDisposePort(ref _controlPort, OnControlDataReceived);
        CloseAndDisposePort(ref _bluetoothControlPort, OnBluetoothControlDataReceived);
        CloseWifiControl();
        _activeControlTransport = "None";
        _inRfidDecoder.Reset();
        _outRfidDecoder.Reset();

        lock (_controlReadSync)
        {
            _controlReadBuffer.Clear();
        }

        lock (_rfidPublishSync)
        {
            _lastPublishedRfids.Clear();
        }
    }

    private static void CloseAndDisposePort(
        ref SerialPort? port,
        SerialDataReceivedEventHandler dataReceivedHandler)
    {
        var currentPort = port;
        port = null;

        if (currentPort is null)
        {
            return;
        }

        try
        {
            currentPort.DataReceived -= dataReceivedHandler;
            if (currentPort.IsOpen)
            {
                currentPort.Close();
            }
        }
        catch
        {
            // Reconfiguration and shutdown must continue even if a driver is unresponsive.
        }
        finally
        {
            currentPort.Dispose();
        }
    }

    private void PublishStatus(bool isReady, string status)
    {
        _isReady = isReady;
        _currentStatus = status;
        ConnectionStatusChanged?.Invoke(
            this,
            new HardwareConnectionStatusChangedEventArgs(isReady, status));
    }

    private static void ValidatePortSettings(HardwareOptions settings)
    {
        var ports = new List<string>
        {
            settings.InRfid.PortName?.Trim() ?? string.Empty,
            settings.OutRfid.PortName?.Trim() ?? string.Empty,
            settings.Control.PortName?.Trim() ?? string.Empty
        };

        if (settings.BluetoothControlEnabled)
        {
            ports.Add(settings.BluetoothControl.PortName?.Trim() ?? string.Empty);
        }

        if (ports.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                settings.BluetoothControlEnabled
                    ? "IN RFID, OUT RFID, wired CONTROL, and Bluetooth backup COM port names are required."
                    : "IN RFID, OUT RFID, and wired CONTROL COM port names are required.");
        }

        if (ports.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ports.Count)
        {
            throw new InvalidOperationException(
                "IN RFID, OUT RFID, wired CONTROL, and Bluetooth backup must use different COM ports.");
        }

        ValidateSerialPort(settings.InRfid, "IN RFID");
        ValidateSerialPort(settings.OutRfid, "OUT RFID");
        ValidateSerialPort(settings.Control, "CONTROL");
        if (settings.BluetoothControlEnabled)
        {
            ValidateSerialPort(settings.BluetoothControl, "BLUETOOTH CONTROL");
        }
        if (settings.WifiControlEnabled)
        {
            if (string.IsNullOrWhiteSpace(settings.WifiControl.Host))
                throw new InvalidOperationException("Wi-Fi control host/IP is required.");
            if (settings.WifiControl.Port is < 1 or > 65535)
                throw new InvalidOperationException("Wi-Fi control TCP port must be between 1 and 65535.");
        }
    }

    private static void ValidateSerialPort(SerialPortOptions settings, string name)
    {
        if (settings.BaudRate <= 0)
        {
            throw new InvalidOperationException($"{name} baud rate must be greater than zero.");
        }

        if (settings.DataBits is < 5 or > 8)
        {
            throw new InvalidOperationException($"{name} data bits must be between 5 and 8.");
        }

        _ = Enum.Parse<Parity>(settings.Parity, true);
        _ = Enum.Parse<StopBits>(settings.StopBits, true);

        if (!string.Equals(
                settings.ReadMode,
                "LineText",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                settings.ReadMode,
                "UhfCfFrame",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{name} read mode must be LineText or UhfCfFrame.");
        }
    }

    private static void CopyHardwareOptions(HardwareOptions source, HardwareOptions destination)
    {
        destination.SimulationEnabled = source.SimulationEnabled;
        destination.LineTerminator = source.LineTerminator;
        destination.ReconnectSeconds = source.ReconnectSeconds;
        CopySerialPortOptions(source.InRfid, destination.InRfid);
        CopySerialPortOptions(source.OutRfid, destination.OutRfid);
        CopySerialPortOptions(source.Control, destination.Control);
        destination.BluetoothControlEnabled = source.BluetoothControlEnabled;
        CopySerialPortOptions(source.BluetoothControl, destination.BluetoothControl);
        destination.WifiControlEnabled = source.WifiControlEnabled;
        destination.WifiControl.Host = source.WifiControl.Host.Trim();
        destination.WifiControl.Port = source.WifiControl.Port;
        destination.WifiControl.ConnectTimeoutMilliseconds = source.WifiControl.ConnectTimeoutMilliseconds;
        destination.SensorMessages.InHigh = source.SensorMessages.InHigh.Trim();
        destination.SensorMessages.InReleased = source.SensorMessages.InReleased.Trim();
        destination.SensorMessages.OutHigh = source.SensorMessages.OutHigh.Trim();
        destination.SensorMessages.OutReleased = source.SensorMessages.OutReleased.Trim();
    }

    private static void CopySerialPortOptions(SerialPortOptions source, SerialPortOptions destination)
    {
        destination.PortName = source.PortName.Trim();
        destination.ReadMode = string.IsNullOrWhiteSpace(source.ReadMode)
            ? "LineText"
            : source.ReadMode.Trim();
        destination.BaudRate = source.BaudRate;
        destination.DataBits = source.DataBits;
        destination.Parity = source.Parity;
        destination.StopBits = source.StopBits;
    }

    private static int GetPortSortNumber(string portName)
    {
        const string prefix = "COM";
        return portName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(portName[prefix.Length..], out var number)
            ? number
            : int.MaxValue;
    }

    private static string DecodeEscapes(string value) => value
        .Replace("\\r", "\r", StringComparison.Ordinal)
        .Replace("\\n", "\n", StringComparison.Ordinal)
        .Replace("\\t", "\t", StringComparison.Ordinal);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HardwareGateway));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _started = false;
        _reconnectCts?.Cancel();
        CloseAndDisposePorts();
        _reconnectCts?.Dispose();
        _lifecycleLock.Dispose();
        _controlWriteLock.Dispose();
    }
}
