using System.Windows;
using RfidVehicleAccess.Services;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class MainWindow : Window
{
    private readonly IawsMainViewModel _viewModel;
    private readonly IawsCameraStreamService _cameraStreams;
    private readonly AppOptions _options;
    private readonly IawsHardwareGateway _hardware;
    private readonly AppConfigurationService _configurationService;

    // Full iTOLL production backend kept available behind the identical menu
    // structure requested for iAWS.
    private readonly MainViewModel _tollViewModel;
    private readonly CameraStreamService _tollCameraStreams;
    private readonly HardwareGateway _tollHardwareGateway;
    private readonly StorageCleanupWorker _storageCleanup;

    public MainWindow(
        IawsMainViewModel viewModel,
        IawsCameraStreamService cameraStreams,
        AppOptions options,
        IawsHardwareGateway hardware,
        AppConfigurationService configurationService,
        MainViewModel tollViewModel,
        CameraStreamService tollCameraStreams,
        HardwareGateway tollHardwareGateway,
        StorageCleanupWorker storageCleanup)
    {
        InitializeComponent();

        AboutVersionMenuItem.Header =
            $"RealTech iAWS Automatic Weighing System v{ApplicationVersionService.GetDisplayVersion()}";

        _viewModel = viewModel;
        _cameraStreams = cameraStreams;
        _options = options;
        _hardware = hardware;
        _configurationService = configurationService;
        _tollViewModel = tollViewModel;
        _tollCameraStreams = tollCameraStreams;
        _tollHardwareGateway = tollHardwareGateway;
        _storageCleanup = storageCleanup;

        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FrontVideoView.MediaPlayer = _cameraStreams.FrontMediaPlayer;
        BackVideoView.MediaPlayer = _cameraStreams.BackMediaPlayer;
        LeftVideoView.MediaPlayer = _cameraStreams.LeftMediaPlayer;
        RightVideoView.MediaPlayer = _cameraStreams.RightMediaPlayer;
        _cameraStreams.Start();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new IawsSettingsWindow(
            _options,
            _hardware,
            _cameraStreams,
            _configurationService,
            _viewModel)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void OpenServerPanel_Click(object sender, RoutedEventArgs e)
    {
        var passwordDialog = new AdminPasswordWindow(
            _options.Security.ServerPanelPassword,
            "Server Panel",
            "Enter the server panel password to edit application configuration settings.")
        {
            Owner = this
        };

        if (passwordDialog.ShowDialog() != true)
        {
            return;
        }

        var dialog = new ServerPanelWindow(
            _options,
            _configurationService,
            _tollHardwareGateway,
            _tollCameraStreams,
            _tollViewModel,
            _storageCleanup)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void OpenPaymentConfiguration_Click(object sender, RoutedEventArgs e)
    {
        var passwordDialog = new AdminPasswordWindow(
            _options.Security.PaymentConfigurationPassword,
            "Payment Configuration",
            "Enter the payment configuration password to manage vehicle categories and prices.")
        {
            Owner = this
        };

        if (passwordDialog.ShowDialog() != true)
        {
            return;
        }

        var dialog = new PaymentConfigurationWindow(
            _options,
            _configurationService)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void ImportVehicles_Click(object sender, RoutedEventArgs e)
    {
        if (_tollViewModel.ImportVehiclesCommand.CanExecute(null))
        {
            _tollViewModel.ImportVehiclesCommand.Execute(null);
        }
    }

    private void ImportVehiclesFromApi_Click(object sender, RoutedEventArgs e)
    {
        if (_tollViewModel.ImportVehiclesFromApiCommand.CanExecute(null))
        {
            _tollViewModel.ImportVehiclesFromApiCommand.Execute(null);
        }
    }

    private void ClearPendingData_Click(object sender, RoutedEventArgs e)
    {
        if (_tollViewModel.ClearPendingDataCommand.CanExecute(null))
        {
            _tollViewModel.ClearPendingDataCommand.Execute(null);
        }
    }

    private void OpenHardwareSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HardwareSettingsWindow(
            _options,
            _tollHardwareGateway,
            _configurationService)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void OpenAdminControls_Click(object sender, RoutedEventArgs e)
    {
        var passwordDialog = new AdminPasswordWindow(
            _options.Security.AdminControlsPassword)
        {
            Owner = this
        };

        if (passwordDialog.ShowDialog() != true)
        {
            return;
        }

        var dialog = new AdminControlsWindow(_tollViewModel)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    protected override void OnClosed(EventArgs e)
    {
        Loaded -= OnLoaded;

        _cameraStreams.Stop();
        FrontVideoView.MediaPlayer = null;
        BackVideoView.MediaPlayer = null;
        LeftVideoView.MediaPlayer = null;
        RightVideoView.MediaPlayer = null;
        FrontVideoView.Dispose();
        BackVideoView.Dispose();
        LeftVideoView.Dispose();
        RightVideoView.Dispose();

        // The iTOLL camera service is not attached to the four-camera dashboard,
        // but it can be started/reconfigured by the Server Panel. Ensure it is
        // stopped during normal application shutdown.
        _tollCameraStreams.Stop();

        _viewModel.Dispose();
        _tollViewModel.Dispose();
        base.OnClosed(e);
    }
}
