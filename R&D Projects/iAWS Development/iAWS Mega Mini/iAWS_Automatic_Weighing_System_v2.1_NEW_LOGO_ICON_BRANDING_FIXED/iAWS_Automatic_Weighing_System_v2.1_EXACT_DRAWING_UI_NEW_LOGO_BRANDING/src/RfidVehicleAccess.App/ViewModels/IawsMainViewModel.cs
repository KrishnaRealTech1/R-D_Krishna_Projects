using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RfidVehicleAccess.Models;
using RfidVehicleAccess.Services;

namespace RfidVehicleAccess.ViewModels;

public sealed class IawsMainViewModel : ViewModelBase, IDisposable
{
    private readonly AppOptions _options;
    private readonly IawsHardwareGateway _hardware;
    private readonly IawsWeighingProcessor _processor;
    private readonly IawsCameraStreamService _cameraStreams;
    private readonly SystemEventHub _eventHub;
    private readonly DispatcherTimer _clockTimer;

    private string _systemTime = DateTime.Now.ToString("dd MMM yyyy  hh:mm:ss tt");
    private decimal _currentWeightKg;
    private string _currentRfid = "WAITING";
    private string _processStatus = "READY";
    private string _processMessage = "Waiting for weight to reach the configured trigger.";
    private string _apiResponse = "-";
    private string _hardwareStatus = "Starting...";
    private string _frontCameraStatus = "Not started";
    private string _backCameraStatus = "Not started";
    private string _leftCameraStatus = "Not started";
    private string _rightCameraStatus = "Not started";
    private string _lastFrontImage = "-";
    private string _lastBackImage = "-";
    private string _lastLeftImage = "-";
    private string _lastRightImage = "-";
    private string _simulatedRfid = "RF00123456";
    private string _simulatedWeight = "0";

    public IawsMainViewModel(
        AppOptions options,
        IawsHardwareGateway hardware,
        IawsWeighingProcessor processor,
        IawsCameraStreamService cameraStreams,
        SystemEventHub eventHub)
    {
        _options = options;
        _hardware = hardware;
        _processor = processor;
        _cameraStreams = cameraStreams;
        _eventHub = eventHub;

        Logs = [];
        StatusLogs = [];
        ServerLogs = [];
        SimulateRfidCommand = new RelayCommand(SimulateRfid);
        SimulateWeightCommand = new RelayCommand(SimulateWeight);
        ResetCycleCommand = new RelayCommand(_processor.ResetCycle);
        OpenCaptureFolderCommand = new RelayCommand(OpenCaptureFolder);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

        _hardware.WeightChanged += OnWeightChanged;
        _hardware.RfidReceived += OnRfidReceived;
        _hardware.ConnectionStatusChanged += OnHardwareStatusChanged;
        _processor.StateChanged += OnProcessorStateChanged;
        _cameraStreams.StatusChanged += OnCameraStatusChanged;
        _eventHub.LogAdded += OnLogAdded;

        _hardwareStatus = _hardware.CurrentStatus;
        _currentWeightKg = _hardware.CurrentWeightKg;
        var currentState = _processor.CurrentState;
        _processStatus = currentState.Status;
        _processMessage = currentState.Message;
        _currentRfid = string.IsNullOrWhiteSpace(currentState.Rfid) ? "WAITING" : currentState.Rfid;
        _apiResponse = string.IsNullOrWhiteSpace(currentState.ApiResponse) ? "-" : currentState.ApiResponse;
        _lastFrontImage = string.IsNullOrWhiteSpace(currentState.FrontImage) ? "-" : currentState.FrontImage;
        _lastBackImage = string.IsNullOrWhiteSpace(currentState.BackImage) ? "-" : currentState.BackImage;
        _lastLeftImage = string.IsNullOrWhiteSpace(currentState.LeftImage) ? "-" : currentState.LeftImage;
        _lastRightImage = string.IsNullOrWhiteSpace(currentState.RightImage) ? "-" : currentState.RightImage;
        _frontCameraStatus = _cameraStreams.GetStatus(IawsCameraPosition.Front);
        _backCameraStatus = _cameraStreams.GetStatus(IawsCameraPosition.Back);
        _leftCameraStatus = _cameraStreams.GetStatus(IawsCameraPosition.Left);
        _rightCameraStatus = _cameraStreams.GetStatus(IawsCameraPosition.Right);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => SystemTime = DateTime.Now.ToString("dd MMM yyyy  hh:mm:ss tt");
        _clockTimer.Start();
    }

    // Logs is kept for backward compatibility with the previous dashboard.
    // The v2.1 hand-drawn layout shows API/server and system/status logs separately.
    public ObservableCollection<string> Logs { get; }
    public ObservableCollection<string> StatusLogs { get; }
    public ObservableCollection<string> ServerLogs { get; }

    public string SystemTime
    {
        get => _systemTime;
        private set => SetProperty(ref _systemTime, value);
    }

    public decimal CurrentWeightKg
    {
        get => _currentWeightKg;
        private set
        {
            if (SetProperty(ref _currentWeightKg, value))
            {
                OnPropertyChanged(nameof(CurrentWeightText));
                OnPropertyChanged(nameof(WeightReached));
                OnPropertyChanged(nameof(WeightStatusText));
            }
        }
    }

    public string CurrentWeightText => $"{CurrentWeightKg:0.##} kg";
    public decimal TriggerWeightKg => _options.Iaws.TriggerWeightKg;
    public decimal ResetWeightKg => _options.Iaws.ResetWeightKg;
    public bool WeightReached => CurrentWeightKg >= TriggerWeightKg;
    public string WeightStatusText => WeightReached ? "TRIGGER REACHED" : "WAITING FOR WEIGHT";
    public string ApiEndpoint => _options.Iaws.ApiEndpoint;
    public string SitePrefix => _options.Iaws.SitePrefix;
    public bool SimulationEnabled => _options.Hardware.SimulationEnabled;

    // Presentation-only state for the new iAWS dashboard. These properties do not
    // alter the existing weighing, RFID, camera or API workflow.
    public bool IsProcessReady =>
        ProcessStatus.Equals("READY", StringComparison.OrdinalIgnoreCase) ||
        ProcessStatus.Equals("RFID READY", StringComparison.OrdinalIgnoreCase) ||
        ProcessStatus.Equals("PROCESSED", StringComparison.OrdinalIgnoreCase);

    public bool IsProcessBusy =>
        ProcessStatus.Equals("WAITING FOR RFID", StringComparison.OrdinalIgnoreCase) ||
        ProcessStatus.Equals("CAPTURING 4 CAMERAS", StringComparison.OrdinalIgnoreCase) ||
        ProcessStatus.Equals("SENDING API", StringComparison.OrdinalIgnoreCase);

    public bool HasProcessError =>
        ProcessStatus.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
        ProcessStatus.Contains("FAILED", StringComparison.OrdinalIgnoreCase);

    public bool IsHardwareReady
    {
        get
        {
            if (SimulationEnabled)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(HardwareStatus))
            {
                return false;
            }

            return !HardwareStatus.Contains("offline", StringComparison.OrdinalIgnoreCase) &&
                   !HardwareStatus.Contains("failed", StringComparison.OrdinalIgnoreCase) &&
                   !HardwareStatus.Contains("starting", StringComparison.OrdinalIgnoreCase) &&
                   !HardwareStatus.Contains("not started", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsWeightConnected =>
        SimulationEnabled ||
        (HardwareStatus.Contains("Weight ", StringComparison.OrdinalIgnoreCase) &&
         !HardwareStatus.Contains("Weight offline", StringComparison.OrdinalIgnoreCase));

    public string WeightConnectivityText => SimulationEnabled
        ? "SIMULATION"
        : IsWeightConnected ? "CONNECTED" : "OFFLINE";

    public string ServerConnectivityText =>
        string.IsNullOrWhiteSpace(ApiEndpoint)
            ? "NOT CONFIGURED"
            : ProcessStatus.Equals("SENDING API", StringComparison.OrdinalIgnoreCase)
                ? "SENDING..."
                : ProcessStatus.Equals("PROCESSED", StringComparison.OrdinalIgnoreCase)
                    ? "LAST API OK"
                    : ProcessStatus.Equals("API FAILED", StringComparison.OrdinalIgnoreCase)
                        ? "LAST API FAILED"
                        : "API READY";

    public string CurrentRfid
    {
        get => _currentRfid;
        private set => SetProperty(ref _currentRfid, value);
    }

    public string ProcessStatus
    {
        get => _processStatus;
        private set
        {
            if (SetProperty(ref _processStatus, value))
            {
                OnPropertyChanged(nameof(IsProcessReady));
                OnPropertyChanged(nameof(IsProcessBusy));
                OnPropertyChanged(nameof(HasProcessError));
                OnPropertyChanged(nameof(ServerConnectivityText));
            }
        }
    }

    public string ProcessMessage
    {
        get => _processMessage;
        private set => SetProperty(ref _processMessage, value);
    }

    public string ApiResponse
    {
        get => _apiResponse;
        private set => SetProperty(ref _apiResponse, value);
    }

    public string HardwareStatus
    {
        get => _hardwareStatus;
        private set
        {
            if (SetProperty(ref _hardwareStatus, value))
            {
                OnPropertyChanged(nameof(IsHardwareReady));
                OnPropertyChanged(nameof(IsWeightConnected));
                OnPropertyChanged(nameof(WeightConnectivityText));
            }
        }
    }

    public string FrontCameraStatus
    {
        get => _frontCameraStatus;
        private set => SetProperty(ref _frontCameraStatus, value);
    }

    public string BackCameraStatus
    {
        get => _backCameraStatus;
        private set => SetProperty(ref _backCameraStatus, value);
    }

    public string LeftCameraStatus
    {
        get => _leftCameraStatus;
        private set => SetProperty(ref _leftCameraStatus, value);
    }

    public string RightCameraStatus
    {
        get => _rightCameraStatus;
        private set => SetProperty(ref _rightCameraStatus, value);
    }

    public string LastFrontImage
    {
        get => _lastFrontImage;
        private set => SetProperty(ref _lastFrontImage, value);
    }

    public string LastBackImage
    {
        get => _lastBackImage;
        private set => SetProperty(ref _lastBackImage, value);
    }

    public string LastLeftImage
    {
        get => _lastLeftImage;
        private set => SetProperty(ref _lastLeftImage, value);
    }

    public string LastRightImage
    {
        get => _lastRightImage;
        private set => SetProperty(ref _lastRightImage, value);
    }

    public string SimulatedRfid
    {
        get => _simulatedRfid;
        set => SetProperty(ref _simulatedRfid, value);
    }

    public string SimulatedWeight
    {
        get => _simulatedWeight;
        set => SetProperty(ref _simulatedWeight, value);
    }

    public ICommand SimulateRfidCommand { get; }
    public ICommand SimulateWeightCommand { get; }
    public ICommand ResetCycleCommand { get; }
    public ICommand OpenCaptureFolderCommand { get; }
    public ICommand ExitCommand { get; }

    public void RefreshConfigurationBindings()
    {
        OnPropertyChanged(nameof(TriggerWeightKg));
        OnPropertyChanged(nameof(ResetWeightKg));
        OnPropertyChanged(nameof(ApiEndpoint));
        OnPropertyChanged(nameof(SitePrefix));
        OnPropertyChanged(nameof(SimulationEnabled));
        OnPropertyChanged(nameof(IsHardwareReady));
        OnPropertyChanged(nameof(IsWeightConnected));
        OnPropertyChanged(nameof(WeightConnectivityText));
        OnPropertyChanged(nameof(ServerConnectivityText));
        OnPropertyChanged(nameof(WeightReached));
        OnPropertyChanged(nameof(WeightStatusText));
    }

    private void SimulateRfid()
    {
        if (!string.IsNullOrWhiteSpace(SimulatedRfid))
        {
            _hardware.SimulateRfid(SimulatedRfid);
        }
    }

    private void SimulateWeight()
    {
        if (decimal.TryParse(SimulatedWeight, out var weight))
        {
            _hardware.SimulateWeight(Math.Max(0m, weight));
        }
    }

    private void OpenCaptureFolder()
    {
        var folder = PathResolver.EnsureDirectory(_options.Cameras.LocalImageFolder);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch
        {
            // The dashboard remains usable even if Explorer cannot be launched.
        }
    }

    private void OnWeightChanged(object? sender, WeightChangedEventArgs e) =>
        OnUi(() => CurrentWeightKg = e.WeightKg);

    private void OnRfidReceived(object? sender, RfidReadEventArgs e) =>
        OnUi(() => CurrentRfid = e.Rfid);

    private void OnHardwareStatusChanged(object? sender, string status) =>
        OnUi(() => HardwareStatus = status);

    private void OnProcessorStateChanged(object? sender, IawsTransactionState state) =>
        OnUi(() =>
        {
            ProcessStatus = state.Status;
            ProcessMessage = state.Message;
            if (!string.IsNullOrWhiteSpace(state.Rfid))
            {
                CurrentRfid = state.Rfid;
            }
            ApiResponse = string.IsNullOrWhiteSpace(state.ApiResponse) ? "-" : state.ApiResponse;
            LastFrontImage = string.IsNullOrWhiteSpace(state.FrontImage) ? "-" : state.FrontImage;
            LastBackImage = string.IsNullOrWhiteSpace(state.BackImage) ? "-" : state.BackImage;
            LastLeftImage = string.IsNullOrWhiteSpace(state.LeftImage) ? "-" : state.LeftImage;
            LastRightImage = string.IsNullOrWhiteSpace(state.RightImage) ? "-" : state.RightImage;
        });

    private void OnCameraStatusChanged(object? sender, IawsCameraStatusChangedEventArgs e) =>
        OnUi(() =>
        {
            switch (e.Position)
            {
                case IawsCameraPosition.Front:
                    FrontCameraStatus = e.Status;
                    break;
                case IawsCameraPosition.Back:
                    BackCameraStatus = e.Status;
                    break;
                case IawsCameraPosition.Left:
                    LeftCameraStatus = e.Status;
                    break;
                case IawsCameraPosition.Right:
                    RightCameraStatus = e.Status;
                    break;
            }
        });

    private void OnLogAdded(object? sender, AppLogEntry entry) =>
        OnUi(() =>
        {
            Logs.Insert(0, entry.DisplayText);
            TrimLog(Logs);

            var destination = entry.Channel == LogChannel.Api ? ServerLogs : StatusLogs;
            destination.Insert(0, entry.DisplayText);
            TrimLog(destination);
        });

    private static void TrimLog(ObservableCollection<string> log)
    {
        while (log.Count > 300)
        {
            log.RemoveAt(log.Count - 1);
        }
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _ = dispatcher.BeginInvoke(action);
        }
    }

    public void Dispose()
    {
        _clockTimer.Stop();
        _hardware.WeightChanged -= OnWeightChanged;
        _hardware.RfidReceived -= OnRfidReceived;
        _hardware.ConnectionStatusChanged -= OnHardwareStatusChanged;
        _processor.StateChanged -= OnProcessorStateChanged;
        _cameraStreams.StatusChanged -= OnCameraStatusChanged;
        _eventHub.LogAdded -= OnLogAdded;
    }
}
