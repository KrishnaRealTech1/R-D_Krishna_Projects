using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using RfidVehicleAccess.Data;
using RfidVehicleAccess.Models;
using RfidVehicleAccess.Services;

namespace RfidVehicleAccess.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly AppOptions _options;
    private readonly SystemEventHub _eventHub;
    private readonly HardwareGateway _hardware;
    private readonly VehicleImportService _importService;
    private readonly VehicleApiImportService _apiImportService;
    private readonly VehicleRepository _vehicleRepository;
    private readonly TripRepository _tripRepository;
    private readonly TripSyncCoordinator _tripSyncCoordinator;
    private readonly AppLogger _logger;
    private readonly CameraStreamService _cameraStreams;
    private readonly AppConfigurationService _configurationService;
    private readonly DispatcherTimer _clockTimer;
    private DateOnly _counterDate = DateOnly.FromDateTime(DateTime.Now);

    private string _systemTime = DateTime.Now.ToString("hh:mm:ss tt");
    private string _inVehicleNumber = "WAITING";
    private string _inRfid = "-";
    private string _inAccessType = "-";
    private string _inBalance = "-";
    private string _inAccessStatus = "Waiting for vehicle";
    private LaneState _inLaneState = LaneState.Idle;
    private string _outVehicleNumber = "WAITING";
    private string _outRfid = "-";
    private string _outAccessType = "-";
    private string _outBalance = "-";
    private string _outAccessStatus = "Waiting for vehicle";
    private LaneState _outLaneState = LaneState.Idle;
    private string _inRfidInput = string.Empty;
    private string _outRfidInput = string.Empty;
    private string _networkStatus = "Checking internet...";
    private bool _isOnline;
    private string _serverStatus = "Checking server...";
    private bool _isServerConnected;
    private bool _isServerPartiallyConnected;
    private SignalLightState _inSignalState = SignalLightState.Green;
    private SignalLightState _outSignalState = SignalLightState.Green;
    private bool _isInBuzzerActive;
    private bool _isOutBuzzerActive;
    private bool _isInBarrierOpen;
    private bool _isOutBarrierOpen;
    private string _processedRecords = "0";
    private string _pendingRecords = "0";
    private string _inCameraStatus = "Connecting...";
    private string _outCameraStatus = "Connecting...";
    private string _hardwareStatus = "Hardware starting...";
    private string _activeControlTransport = "None";
    private bool _isHardwareReady;
    private bool _isInExceptionalApprovalEnabled;
    private bool _isOutExceptionalApprovalEnabled;
    private bool _isInAutoApprovalEnabled;
    private bool _isOutAutoApprovalEnabled;
    private bool _isRedFrontendEnabled;
    private bool _isGreenFrontendEnabled;
    private bool _isOrangeFrontendEnabled;
    private bool _isBuzzerFrontendEnabled;

    public MainViewModel(
        AppOptions options,
        SystemEventHub eventHub,
        HardwareGateway hardware,
        VehicleImportService importService,
        VehicleApiImportService apiImportService,
        VehicleRepository vehicleRepository,
        TripRepository tripRepository,
        TripSyncCoordinator tripSyncCoordinator,
        AppLogger logger,
        CameraStreamService cameraStreams,
        AppConfigurationService configurationService)
    {
        _options = options;
        _eventHub = eventHub;
        _hardware = hardware;
        _importService = importService;
        _apiImportService = apiImportService;
        _vehicleRepository = vehicleRepository;
        _tripRepository = tripRepository;
        _tripSyncCoordinator = tripSyncCoordinator;
        _logger = logger;
        _cameraStreams = cameraStreams;
        _configurationService = configurationService;
        _hardwareStatus = _hardware.CurrentStatus;
        _activeControlTransport = _hardware.ActiveControlTransport;
        _isHardwareReady = _hardware.IsReady;
        _isInExceptionalApprovalEnabled = _options.ExceptionalApproval.InEnabled;
        _isOutExceptionalApprovalEnabled = _options.ExceptionalApproval.OutEnabled;
        _isInAutoApprovalEnabled = _options.AutoApproval.InEnabled;
        _isOutAutoApprovalEnabled = _options.AutoApproval.OutEnabled;
        _isRedFrontendEnabled = _options.FrontendIndicators.RedEnabled;
        _isGreenFrontendEnabled = _options.FrontendIndicators.GreenEnabled;
        _isOrangeFrontendEnabled = _options.FrontendIndicators.OrangeEnabled;
        _isBuzzerFrontendEnabled = _options.FrontendIndicators.BuzzerEnabled;

        _inCameraStatus = _options.Cameras.In.Enabled
            ? _cameraStreams.InStatus
            : "Camera disabled";
        _outCameraStatus = _options.Cameras.Out.Enabled
            ? _cameraStreams.OutStatus
            : "Camera disabled";

        StatusLogs = [];
        ServerLogs = [];

        ImportVehiclesCommand = new AsyncRelayCommand(ImportVehiclesAsync);
        ImportVehiclesFromApiCommand = new AsyncRelayCommand(ImportVehiclesFromApiAsync);
        ClearPendingDataCommand = new AsyncRelayCommand(ClearPendingDataAsync);
        SaveAdminSettingsCommand = new AsyncRelayCommand(SaveAdminSettingsAsync);
        SaveExceptionalApprovalSettingsCommand = SaveAdminSettingsCommand;
        OpenConfigurationCommand = new RelayCommand(OpenConfiguration);
        OpenCaptureFolderCommand = new RelayCommand(OpenCaptureFolder);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
        SimulateInSensorHighCommand = new RelayCommand(
            () => _hardware.SimulateSensor(LaneDirection.In, true));
        SimulateInReleasedCommand = new RelayCommand(
            () => _hardware.SimulateSensor(LaneDirection.In, false));
        SimulateOutSensorHighCommand = new RelayCommand(
            () => _hardware.SimulateSensor(LaneDirection.Out, true));
        SimulateOutReleasedCommand = new RelayCommand(
            () => _hardware.SimulateSensor(LaneDirection.Out, false));
        SimulateInRfidCommand = new RelayCommand(
            () => _hardware.SimulateRfid(LaneDirection.In, InRfidInput),
            () => !string.IsNullOrWhiteSpace(InRfidInput));
        SimulateOutRfidCommand = new RelayCommand(
            () => _hardware.SimulateRfid(LaneDirection.Out, OutRfidInput),
            () => !string.IsNullOrWhiteSpace(OutRfidInput));

        OpenInBarrierCommand = CreateControlCommand("OPEN IN BB");
        CloseInBarrierCommand = CreateControlCommand("CLOSE IN BB");
        OpenOutBarrierCommand = CreateControlCommand("OPEN OUT BB");
        CloseOutBarrierCommand = CreateControlCommand("CLOSE OUT BB");
        InRedCommand = CreateControlCommand("IN RED");
        InOrangeCommand = CreateControlCommand("IN ORG");
        InGreenCommand = CreateControlCommand("IN GRN");
        InBuzzerCommand = CreateControlCommand("IN Buzzer");
        InBuzzerOffCommand = CreateControlCommand("IN Buzzer OFF");
        InAllOffCommand = CreateControlCommand("IN ALL OFF");
        OutRedCommand = CreateControlCommand("OUT RED");
        OutOrangeCommand = CreateControlCommand("OUT ORG");
        OutGreenCommand = CreateControlCommand("OUT GRN");
        OutBuzzerCommand = CreateControlCommand("OUT Buzzer");
        OutBuzzerOffCommand = CreateControlCommand("OUT Buzzer OFF");
        OutAllOffCommand = CreateControlCommand("OUT ALL OFF");

        _eventHub.LogAdded += OnLogAdded;
        _eventHub.LaneStateChanged += OnLaneStateChanged;
        _eventHub.CountersChanged += OnCountersChanged;
        _eventHub.InternetConnectivityChanged += OnInternetConnectivityChanged;
        _eventHub.ServerConnectivityChanged += OnServerConnectivityChanged;
        _hardware.ControlCommandSent += OnControlCommandSent;
        _hardware.ConnectionStatusChanged += OnHardwareConnectionStatusChanged;
        _cameraStreams.StatusChanged += OnCameraStatusChanged;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += OnClockTimerTick;
        _clockTimer.Start();

        _ = RefreshCountersAsync();
        _ = _logger.StatusAsync(
            $"UI started for site {_options.Device.SiteId}, device {_options.Device.DeviceId}.");
    }

    public ObservableCollection<string> StatusLogs { get; }
    public ObservableCollection<string> ServerLogs { get; }

    public string SystemTime
    {
        get => _systemTime;
        set => SetProperty(ref _systemTime, value);
    }

    public string InVehicleNumber
    {
        get => _inVehicleNumber;
        set => SetProperty(ref _inVehicleNumber, value);
    }

    public string InRfid
    {
        get => _inRfid;
        set => SetProperty(ref _inRfid, value);
    }

    public string InAccessType
    {
        get => _inAccessType;
        set => SetProperty(ref _inAccessType, value);
    }

    public string InBalance
    {
        get => _inBalance;
        set => SetProperty(ref _inBalance, value);
    }

    public string InAccessStatus
    {
        get => _inAccessStatus;
        set => SetProperty(ref _inAccessStatus, value);
    }

    public LaneState InLaneState
    {
        get => _inLaneState;
        set => SetProperty(ref _inLaneState, value);
    }

    public string OutVehicleNumber
    {
        get => _outVehicleNumber;
        set => SetProperty(ref _outVehicleNumber, value);
    }

    public string OutRfid
    {
        get => _outRfid;
        set => SetProperty(ref _outRfid, value);
    }

    public string OutAccessType
    {
        get => _outAccessType;
        set => SetProperty(ref _outAccessType, value);
    }

    public string OutBalance
    {
        get => _outBalance;
        set => SetProperty(ref _outBalance, value);
    }

    public string OutAccessStatus
    {
        get => _outAccessStatus;
        set => SetProperty(ref _outAccessStatus, value);
    }

    public LaneState OutLaneState
    {
        get => _outLaneState;
        set => SetProperty(ref _outLaneState, value);
    }

    public string InRfidInput
    {
        get => _inRfidInput;
        set
        {
            if (SetProperty(ref _inRfidInput, value))
            {
                ((RelayCommand)SimulateInRfidCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string OutRfidInput
    {
        get => _outRfidInput;
        set
        {
            if (SetProperty(ref _outRfidInput, value))
            {
                ((RelayCommand)SimulateOutRfidCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string NetworkStatus
    {
        get => _networkStatus;
        set => SetProperty(ref _networkStatus, value);
    }

    public bool IsOnline
    {
        get => _isOnline;
        set => SetProperty(ref _isOnline, value);
    }

    public string ServerStatus
    {
        get => _serverStatus;
        set => SetProperty(ref _serverStatus, value);
    }

    public bool IsServerConnected
    {
        get => _isServerConnected;
        set => SetProperty(ref _isServerConnected, value);
    }

    public bool IsServerPartiallyConnected
    {
        get => _isServerPartiallyConnected;
        set => SetProperty(ref _isServerPartiallyConnected, value);
    }

    public SignalLightState InSignalState
    {
        get => _inSignalState;
        set => SetProperty(ref _inSignalState, value);
    }

    public SignalLightState OutSignalState
    {
        get => _outSignalState;
        set => SetProperty(ref _outSignalState, value);
    }

    public bool IsInBuzzerActive
    {
        get => _isInBuzzerActive;
        set => SetProperty(ref _isInBuzzerActive, value);
    }

    public bool IsOutBuzzerActive
    {
        get => _isOutBuzzerActive;
        set => SetProperty(ref _isOutBuzzerActive, value);
    }

    public bool IsInBarrierOpen
    {
        get => _isInBarrierOpen;
        set => SetProperty(ref _isInBarrierOpen, value);
    }

    public bool IsOutBarrierOpen
    {
        get => _isOutBarrierOpen;
        set => SetProperty(ref _isOutBarrierOpen, value);
    }

    public string ProcessedRecords
    {
        get => _processedRecords;
        set => SetProperty(ref _processedRecords, value);
    }

    public string PendingRecords
    {
        get => _pendingRecords;
        set => SetProperty(ref _pendingRecords, value);
    }

    public string InCameraStatus
    {
        get => _inCameraStatus;
        set => SetProperty(ref _inCameraStatus, value);
    }

    public string OutCameraStatus
    {
        get => _outCameraStatus;
        set => SetProperty(ref _outCameraStatus, value);
    }

    public string HardwareStatus
    {
        get => _hardwareStatus;
        set => SetProperty(ref _hardwareStatus, value);
    }

    public string ActiveControlTransport
    {
        get => _activeControlTransport;
        set => SetProperty(ref _activeControlTransport, value);
    }

    public bool IsHardwareReady
    {
        get => _isHardwareReady;
        set => SetProperty(ref _isHardwareReady, value);
    }

    public bool IsInExceptionalApprovalEnabled
    {
        get => _isInExceptionalApprovalEnabled;
        set => SetProperty(ref _isInExceptionalApprovalEnabled, value);
    }

    public bool IsOutExceptionalApprovalEnabled
    {
        get => _isOutExceptionalApprovalEnabled;
        set => SetProperty(ref _isOutExceptionalApprovalEnabled, value);
    }

    public bool IsInAutoApprovalEnabled
    {
        get => _isInAutoApprovalEnabled;
        set => SetProperty(ref _isInAutoApprovalEnabled, value);
    }

    public bool IsOutAutoApprovalEnabled
    {
        get => _isOutAutoApprovalEnabled;
        set => SetProperty(ref _isOutAutoApprovalEnabled, value);
    }

    public bool IsRedFrontendEnabled
    {
        get => _isRedFrontendEnabled;
        set => SetProperty(ref _isRedFrontendEnabled, value);
    }

    public bool IsGreenFrontendEnabled
    {
        get => _isGreenFrontendEnabled;
        set => SetProperty(ref _isGreenFrontendEnabled, value);
    }

    public bool IsOrangeFrontendEnabled
    {
        get => _isOrangeFrontendEnabled;
        set => SetProperty(ref _isOrangeFrontendEnabled, value);
    }

    public bool IsBuzzerFrontendEnabled
    {
        get => _isBuzzerFrontendEnabled;
        set => SetProperty(ref _isBuzzerFrontendEnabled, value);
    }

    public ICommand ImportVehiclesCommand { get; }
    public ICommand ImportVehiclesFromApiCommand { get; }
    public ICommand ClearPendingDataCommand { get; }
    public ICommand SaveAdminSettingsCommand { get; }
    public ICommand SaveExceptionalApprovalSettingsCommand { get; }
    public ICommand OpenConfigurationCommand { get; }
    public ICommand OpenCaptureFolderCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand SimulateInSensorHighCommand { get; }
    public ICommand SimulateInReleasedCommand { get; }
    public ICommand SimulateOutSensorHighCommand { get; }
    public ICommand SimulateOutReleasedCommand { get; }
    public ICommand SimulateInRfidCommand { get; }
    public ICommand SimulateOutRfidCommand { get; }
    public ICommand OpenInBarrierCommand { get; }
    public ICommand CloseInBarrierCommand { get; }
    public ICommand OpenOutBarrierCommand { get; }
    public ICommand CloseOutBarrierCommand { get; }
    public ICommand InRedCommand { get; }
    public ICommand InOrangeCommand { get; }
    public ICommand InGreenCommand { get; }
    public ICommand InBuzzerCommand { get; }
    public ICommand InBuzzerOffCommand { get; }
    public ICommand InAllOffCommand { get; }
    public ICommand OutRedCommand { get; }
    public ICommand OutOrangeCommand { get; }
    public ICommand OutGreenCommand { get; }
    public ICommand OutBuzzerCommand { get; }
    public ICommand OutBuzzerOffCommand { get; }
    public ICommand OutAllOffCommand { get; }

    private ICommand CreateControlCommand(string command) =>
        new AsyncRelayCommand(() => _hardware.SendControlCommandAsync(command));

    public void RefreshExceptionalApprovalSettings()
    {
        IsInExceptionalApprovalEnabled = _options.ExceptionalApproval.InEnabled;
        IsOutExceptionalApprovalEnabled = _options.ExceptionalApproval.OutEnabled;
        IsInAutoApprovalEnabled = _options.AutoApproval.InEnabled;
        IsOutAutoApprovalEnabled = _options.AutoApproval.OutEnabled;
        IsRedFrontendEnabled = _options.FrontendIndicators.RedEnabled;
        IsGreenFrontendEnabled = _options.FrontendIndicators.GreenEnabled;
        IsOrangeFrontendEnabled = _options.FrontendIndicators.OrangeEnabled;
        IsBuzzerFrontendEnabled = _options.FrontendIndicators.BuzzerEnabled;
    }

    private async Task SaveAdminSettingsAsync()
    {
        var previousInExceptionalEnabled = _options.ExceptionalApproval.InEnabled;
        var previousOutExceptionalEnabled = _options.ExceptionalApproval.OutEnabled;
        var previousInAutoEnabled = _options.AutoApproval.InEnabled;
        var previousOutAutoEnabled = _options.AutoApproval.OutEnabled;
        var previousRedFrontendEnabled = _options.FrontendIndicators.RedEnabled;
        var previousGreenFrontendEnabled = _options.FrontendIndicators.GreenEnabled;
        var previousOrangeFrontendEnabled = _options.FrontendIndicators.OrangeEnabled;
        var previousBuzzerFrontendEnabled = _options.FrontendIndicators.BuzzerEnabled;

        try
        {
            _options.ExceptionalApproval.InEnabled = IsInExceptionalApprovalEnabled;
            _options.ExceptionalApproval.OutEnabled = IsOutExceptionalApprovalEnabled;
            _options.AutoApproval.InEnabled = IsInAutoApprovalEnabled;
            _options.AutoApproval.OutEnabled = IsOutAutoApprovalEnabled;
            _options.FrontendIndicators.RedEnabled = IsRedFrontendEnabled;
            _options.FrontendIndicators.GreenEnabled = IsGreenFrontendEnabled;
            _options.FrontendIndicators.OrangeEnabled = IsOrangeFrontendEnabled;
            _options.FrontendIndicators.BuzzerEnabled = IsBuzzerFrontendEnabled;
            await _configurationService.SaveAsync();

            var inExceptionalStatus = ToEnabledText(IsInExceptionalApprovalEnabled);
            var outExceptionalStatus = ToEnabledText(IsOutExceptionalApprovalEnabled);
            var inAutoStatus = ToEnabledText(IsInAutoApprovalEnabled);
            var outAutoStatus = ToEnabledText(IsOutAutoApprovalEnabled);
            var redStatus = ToEnabledText(IsRedFrontendEnabled);
            var greenStatus = ToEnabledText(IsGreenFrontendEnabled);
            var orangeStatus = ToEnabledText(IsOrangeFrontendEnabled);
            var buzzerStatus = ToEnabledText(IsBuzzerFrontendEnabled);

            await _logger.StatusAsync(
                "Admin settings saved. " +
                $"Exceptional IN={inExceptionalStatus}, OUT={outExceptionalStatus}; " +
                $"Auto IN={inAutoStatus}, OUT={outAutoStatus}; " +
                $"Frontend RED={redStatus}, GREEN={greenStatus}, " +
                $"ORANGE={orangeStatus}, BUZZER={buzzerStatus}.");

            var confirmationMessage =
                "Admin settings saved.\n\n" +
                $"IN Exceptional Approval Popup: {inExceptionalStatus.ToUpperInvariant()}\n" +
                $"OUT Exceptional Approval Popup: {outExceptionalStatus.ToUpperInvariant()}\n" +
                $"IN Auto Approval: {inAutoStatus.ToUpperInvariant()}\n" +
                $"OUT Auto Approval: {outAutoStatus.ToUpperInvariant()}\n\n" +
                $"Frontend RED: {redStatus.ToUpperInvariant()}\n" +
                $"Frontend GREEN: {greenStatus.ToUpperInvariant()}\n" +
                $"Frontend ORANGE: {orangeStatus.ToUpperInvariant()}\n" +
                $"Frontend BUZZER: {buzzerStatus.ToUpperInvariant()}";

            MessageBox.Show(
                confirmationMessage,
                "Admin Controls",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _options.ExceptionalApproval.InEnabled = previousInExceptionalEnabled;
            _options.ExceptionalApproval.OutEnabled = previousOutExceptionalEnabled;
            _options.AutoApproval.InEnabled = previousInAutoEnabled;
            _options.AutoApproval.OutEnabled = previousOutAutoEnabled;
            _options.FrontendIndicators.RedEnabled = previousRedFrontendEnabled;
            _options.FrontendIndicators.GreenEnabled = previousGreenFrontendEnabled;
            _options.FrontendIndicators.OrangeEnabled = previousOrangeFrontendEnabled;
            _options.FrontendIndicators.BuzzerEnabled = previousBuzzerFrontendEnabled;
            RefreshExceptionalApprovalSettings();

            await _logger.StatusAsync(
                $"Failed to save admin settings: {ex.Message}");
            MessageBox.Show(
                ex.Message,
                "Unable to Save Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string ToEnabledText(bool enabled) => enabled ? "enabled" : "disabled";

    private async Task ImportVehiclesFromApiAsync()
    {
        var sources = _apiImportService.GetEnabledSources();
        if (sources.Count == 0)
        {
            MessageBox.Show(
                "Configure and enable at least one Vehicle CSV API source in " +
                "Server Panel > Storage & Import before using API synchronization.",
                "Vehicle API Configuration Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var sourceNames = string.Join(
            Environment.NewLine,
            sources.Select(source =>
                $"  [{source.Priority}] {source.Name} ({source.Username})"));
        var confirmation = MessageBox.Show(
            $"Download and import registered vehicles from {sources.Count} enabled API source(s)?" +
            Environment.NewLine + Environment.NewLine +
            sourceNames +
            Environment.NewLine + Environment.NewLine +
            "Existing records are updated only for columns supplied by each API. " +
            "Local balance, access type, active status and category are preserved when omitted.",
            "Sync Registered Vehicles from APIs",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var apiResult = await _apiImportService.ImportAllAsync();
            await RefreshCountersAsync();

            MessageBox.Show(
                apiResult.ToDisplayText(),
                "Vehicle API Import Result",
                MessageBoxButton.OK,
                apiResult.FailedSourceCount > 0
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            await _logger.StatusAsync("Vehicle API import was cancelled.");
            MessageBox.Show(
                "The vehicle API synchronization was cancelled.",
                "Vehicle API Import Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync($"Vehicle API import failed: {ex.Message}");
            MessageBox.Show(
                ex.Message,
                "Vehicle API Import Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ImportVehiclesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import registered RFID vehicles",
            Filter = "Vehicle files (*.csv;*.xlsx;*.xlsm)|*.csv;*.xlsx;*.xlsm|" +
                     "CSV files (*.csv)|*.csv|Excel files (*.xlsx;*.xlsm)|*.xlsx;*.xlsm"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = await _importService.ImportAsync(dialog.FileName);
            await RefreshCountersAsync();

            MessageBox.Show(
                result.ToDisplayText(),
                "Vehicle Import Result",
                MessageBoxButton.OK,
                result.Failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync($"Vehicle import failed: {ex.Message}");
            MessageBox.Show(ex.Message, "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenConfiguration()
    {
        var configurationPath = _configurationService.ConfigurationPath;
        if (!File.Exists(configurationPath))
        {
            MessageBox.Show(
                "The application configuration file could not be found.",
                "Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = configurationPath,
            UseShellExecute = true
        });
    }

    private void OpenCaptureFolder()
    {
        var captureFolder = PathResolver.EnsureCaptureStorage();
        Process.Start(new ProcessStartInfo
        {
            FileName = captureFolder,
            UseShellExecute = true
        });
    }

    private void OnLogAdded(object? sender, AppLogEntry entry)
    {
        RunOnUiThread(() =>
        {
            var target = entry.Channel == LogChannel.Status ? StatusLogs : ServerLogs;
            target.Insert(0, entry.DisplayText);
            while (target.Count > 2000)
            {
                target.RemoveAt(target.Count - 1);
            }
        });
    }

    private void OnLaneStateChanged(object? sender, LaneDisplayState state)
    {
        RunOnUiThread(() => UpdateLaneVehicleCard(state));

        if (!string.IsNullOrWhiteSpace(state.RfidNumber))
        {
            _ = LoadVehicleDetailsAsync(state.Direction, state.RfidNumber);
        }
    }

    private void UpdateLaneVehicleCard(LaneDisplayState state)
    {
        var isIdle = state.State == LaneState.Idle;
        var vehicleNumber = string.IsNullOrWhiteSpace(state.VehicleNumber)
            ? isIdle ? "WAITING" : "UNKNOWN"
            : state.VehicleNumber;
        var rfid = string.IsNullOrWhiteSpace(state.RfidNumber) ? "-" : state.RfidNumber;
        var status = string.IsNullOrWhiteSpace(state.Message) ? "Waiting for vehicle" : state.Message;

        if (state.Direction == LaneDirection.In)
        {
            InVehicleNumber = vehicleNumber;
            InRfid = rfid;
            InAccessStatus = status;
            InLaneState = state.State;

            if (isIdle || rfid == "-")
            {
                InAccessType = "-";
                InBalance = "-";
            }

            return;
        }

        OutVehicleNumber = vehicleNumber;
        OutRfid = rfid;
        OutAccessStatus = status;
        OutLaneState = state.State;

        if (isIdle || rfid == "-")
        {
            OutAccessType = "-";
            OutBalance = "-";
        }
    }

    private async Task LoadVehicleDetailsAsync(LaneDirection direction, string rfid)
    {
        var vehicle = await _vehicleRepository.GetByRfidAsync(rfid);
        var accessType = vehicle?.AccessType.ToString().ToUpperInvariant() ?? "NOT REGISTERED";
        var balance = vehicle is null ? "-" : vehicle.Balance.ToString("0.00");

        RunOnUiThread(() =>
        {
            if (direction == LaneDirection.In &&
                string.Equals(InRfid, rfid, StringComparison.OrdinalIgnoreCase))
            {
                InAccessType = accessType;
                InBalance = balance;
            }
            else if (direction == LaneDirection.Out &&
                     string.Equals(OutRfid, rfid, StringComparison.OrdinalIgnoreCase))
            {
                OutAccessType = accessType;
                OutBalance = balance;
            }

        });
    }

    private void OnCountersChanged(object? sender, EventArgs e)
    {
        _ = RefreshCountersAsync();
    }

    private void OnInternetConnectivityChanged(
        object? sender,
        InternetConnectivityStatus connectivityStatus)
    {
        RunOnUiThread(() =>
        {
            IsOnline = connectivityStatus.IsOnline;
            NetworkStatus = connectivityStatus.Status;
        });
    }

    private void OnServerConnectivityChanged(
        object? sender,
        ServerConnectivityStatus connectivityStatus)
    {
        RunOnUiThread(() =>
        {
            IsServerConnected = connectivityStatus.IsConnected;
            IsServerPartiallyConnected = connectivityStatus.IsPartiallyConnected;
            ServerStatus = connectivityStatus.Status;
        });
    }

    private void OnCameraStatusChanged(
        object? sender,
        CameraStatusChangedEventArgs eventArgs)
    {
        RunOnUiThread(() =>
        {
            if (eventArgs.Direction == LaneDirection.In)
            {
                InCameraStatus = eventArgs.Status;
                return;
            }

            OutCameraStatus = eventArgs.Status;
        });
    }



    private void OnHardwareConnectionStatusChanged(
        object? sender,
        HardwareConnectionStatusChangedEventArgs eventArgs)
    {
        RunOnUiThread(() =>
        {
            IsHardwareReady = eventArgs.IsReady;
            HardwareStatus = eventArgs.Status;
            ActiveControlTransport = _hardware.ActiveControlTransport;
        });
    }

    private void OnControlCommandSent(object? sender, string command)
    {
        var normalizedCommand = command.Trim().ToUpperInvariant();

        RunOnUiThread(() =>
        {
            // Automatic RED commands include the detected vehicle number, for
            // example "IN RED TN 56 H 8658". Treat both the detailed and manual
            // forms as the same red-signal state in the dashboard.
            if (normalizedCommand == "IN RED" ||
                normalizedCommand.StartsWith("IN RED ", StringComparison.Ordinal))
            {
                SetSignal(LaneDirection.In, SignalLightState.Red);
                return;
            }

            if (normalizedCommand == "OUT RED" ||
                normalizedCommand.StartsWith("OUT RED ", StringComparison.Ordinal))
            {
                SetSignal(LaneDirection.Out, SignalLightState.Red);
                return;
            }

            switch (normalizedCommand)
            {
                case "OPEN IN BB":
                    IsInBarrierOpen = true;
                    break;
                case "CLOSE IN BB":
                    IsInBarrierOpen = false;
                    break;
                case "OPEN OUT BB":
                    IsOutBarrierOpen = true;
                    break;
                case "CLOSE OUT BB":
                    IsOutBarrierOpen = false;
                    break;
                case "IN ORG":
                    SetSignal(LaneDirection.In, SignalLightState.Orange);
                    break;
                case "IN GRN":
                    SetSignal(LaneDirection.In, SignalLightState.Green);
                    break;
                case "IN BUZZER":
                    SetBuzzerState(LaneDirection.In, true);
                    break;
                case "IN BUZZER OFF":
                    SetBuzzerState(LaneDirection.In, false);
                    break;
                case "IN ALL OFF":
                    SetSignal(LaneDirection.In, SignalLightState.Off);
                    break;
                case "OUT ORG":
                    SetSignal(LaneDirection.Out, SignalLightState.Orange);
                    break;
                case "OUT GRN":
                    SetSignal(LaneDirection.Out, SignalLightState.Green);
                    break;
                case "OUT BUZZER":
                    SetBuzzerState(LaneDirection.Out, true);
                    break;
                case "OUT BUZZER OFF":
                    SetBuzzerState(LaneDirection.Out, false);
                    break;
                case "OUT ALL OFF":
                    SetSignal(LaneDirection.Out, SignalLightState.Off);
                    break;
            }
        });
    }

    private void SetSignal(
        LaneDirection direction,
        SignalLightState signalState)
    {
        if (direction == LaneDirection.In)
        {
            InSignalState = signalState;
        }
        else
        {
            OutSignalState = signalState;
        }

        // The physical sequence enables the buzzer together with ORG and stops
        // it when ORG is cleared. Mirror that latched state in the dashboard.
        SetBuzzerState(direction, signalState == SignalLightState.Orange);
    }

    private void SetBuzzerState(LaneDirection direction, bool isActive)
    {
        if (direction == LaneDirection.In)
        {
            IsInBuzzerActive = isActive;
            return;
        }

        IsOutBuzzerActive = isActive;
    }

    private void OnClockTimerTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        SystemTime = now.ToString("hh:mm:ss tt");

        var currentDate = DateOnly.FromDateTime(now);
        if (currentDate == _counterDate)
        {
            return;
        }

        _counterDate = currentDate;
        _ = RefreshCountersAsync();
    }

    private async Task ClearPendingDataAsync()
    {
        try
        {
            var pendingCount = await _tripRepository.CountPendingAsync();
            if (pendingCount == 0)
            {
                MessageBox.Show(
                    Application.Current.MainWindow,
                    "There are no pending transactions to clear.",
                    "Clear Pending Data",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirmation = MessageBox.Show(
                Application.Current.MainWindow,
                $"This will permanently delete {pendingCount} pending transaction(s) " +
                "and their local captured image files. This action cannot be undone.\n\n" +
                "Make sure no vehicle is currently being processed. Continue?",
                "Clear Pending Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            var result = await _tripSyncCoordinator.RunAsync<(
                int DeletedCount,
                IReadOnlyList<string> ImagePaths)>(
                () => _tripRepository.DeletePendingAsync());

            var deletedImageCount = 0;
            foreach (var imagePath in result.ImagePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                        deletedImageCount++;
                    }
                }
                catch (Exception ex)
                {
                    await _logger.StatusAsync(
                        $"Pending data cleared, but image could not be deleted: {imagePath}. {ex.Message}");
                }
            }

            _eventHub.PublishCountersChanged();
            await RefreshCountersAsync();
            await _logger.StatusAsync(
                $"Operator cleared {result.DeletedCount} pending transaction(s) and " +
                $"{deletedImageCount} local image file(s).");

            MessageBox.Show(
                Application.Current.MainWindow,
                $"Cleared {result.DeletedCount} pending transaction(s).\n" +
                $"Deleted {deletedImageCount} local image file(s).",
                "Pending Data Cleared",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync($"Unable to clear pending data: {ex.Message}");
            MessageBox.Show(
                Application.Current.MainWindow,
                $"Unable to clear pending data.\n\n{ex.Message}",
                "Clear Pending Data",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task RefreshCountersAsync()
    {
        try
        {
            var counts = await _tripRepository.GetDashboardCountsAsync();

            RunOnUiThread(() =>
            {
                ProcessedRecords = counts.ProcessedToday.ToString();
                PendingRecords = counts.Pending.ToString();
            });
        }
        catch (Exception ex)
        {
            await _logger.StatusAsync($"Unable to refresh dashboard counters: {ex.Message}");
        }
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    private static void RunOnUiThread(Func<Task> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            _ = action();
        }
        else
        {
            _ = dispatcher.InvokeAsync(action).Task.Unwrap();
        }
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _clockTimer.Tick -= OnClockTimerTick;
        _eventHub.LogAdded -= OnLogAdded;
        _eventHub.LaneStateChanged -= OnLaneStateChanged;
        _eventHub.CountersChanged -= OnCountersChanged;
        _eventHub.InternetConnectivityChanged -= OnInternetConnectivityChanged;
        _eventHub.ServerConnectivityChanged -= OnServerConnectivityChanged;
        _hardware.ControlCommandSent -= OnControlCommandSent;
        _hardware.ConnectionStatusChanged -= OnHardwareConnectionStatusChanged;
        _cameraStreams.StatusChanged -= OnCameraStatusChanged;
    }
}
