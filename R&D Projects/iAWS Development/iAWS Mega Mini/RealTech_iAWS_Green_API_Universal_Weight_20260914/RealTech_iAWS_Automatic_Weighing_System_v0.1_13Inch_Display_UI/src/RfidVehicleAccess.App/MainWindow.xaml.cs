using System.Windows;
using RfidVehicleAccess.Services;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly CameraStreamService _cameraStreams;
    private readonly AppOptions _options;
    private readonly HardwareGateway _hardwareGateway;
    private readonly WeighbridgeService _weighbridge;
    private readonly AppConfigurationService _configurationService;
    private readonly StorageCleanupWorker _storageCleanup;

    public MainWindow(
        MainViewModel viewModel,
        CameraStreamService cameraStreams,
        AppOptions options,
        HardwareGateway hardwareGateway,
        WeighbridgeService weighbridge,
        AppConfigurationService configurationService,
        StorageCleanupWorker storageCleanup)
    {
        InitializeComponent();
        AboutVersionMenuItem.Header =
            $"RealTech iAWS - Version v{ApplicationVersionService.GetDisplayVersion()}";
        _viewModel = viewModel;
        _cameraStreams = cameraStreams;
        _options = options;
        _hardwareGateway = hardwareGateway;
        _weighbridge = weighbridge;
        _configurationService = configurationService;
        _storageCleanup = storageCleanup;
        DataContext = viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Camera1VideoView.MediaPlayer = _cameraStreams.Camera1MediaPlayer;
        Camera2VideoView.MediaPlayer = _cameraStreams.Camera2MediaPlayer;
        Camera3VideoView.MediaPlayer = _cameraStreams.Camera3MediaPlayer;
        Camera4VideoView.MediaPlayer = _cameraStreams.Camera4MediaPlayer;
        _cameraStreams.Start();
        _viewModel.RefreshWeighbridgeSettings();
    }

    private void OpenHardwareSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HardwareSettingsWindow(
            _options,
            _hardwareGateway,
            _weighbridge,
            _configurationService)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.RefreshWeighbridgeSettings();
        }
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

    private void OpenAdminControls_Click(object sender, RoutedEventArgs e)
    {
        var passwordDialog = new AdminPasswordWindow(
            _options.Security.AdminControlsPassword,
            "iAWS Control Panel",
            "Enter the control-panel password for weighbridge/RFID simulation and manual lane controls.")
        {
            Owner = this
        };

        if (passwordDialog.ShowDialog() != true)
        {
            return;
        }

        var dialog = new AdminControlsWindow(_viewModel)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    private void OpenServerPanel_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthorizeServerPanelAccess(
                "Enter the server-panel password to edit weight, camera and server configuration."))
        {
            return;
        }

        var dialog = new ServerPanelWindow(
            _options,
            _configurationService,
            _hardwareGateway,
            _weighbridge,
            _cameraStreams,
            _viewModel,
            _storageCleanup)
        {
            Owner = this
        };

        dialog.ShowDialog();
        _viewModel.RefreshWeighbridgeSettings();
    }

    private void OpenCoreConfiguration_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthorizeServerPanelAccess(
                "Enter the server-panel password to open the core application configuration."))
        {
            return;
        }

        _viewModel.OpenConfigurationCommand.Execute(null);
    }

    private bool AuthorizeServerPanelAccess(string message)
    {
        var passwordDialog = new AdminPasswordWindow(
            _options.Security.ServerPanelPassword,
            "Server Panel",
            message)
        {
            Owner = this
        };

        return passwordDialog.ShowDialog() == true;
    }

    private void OpenAbout_Click(object sender, RoutedEventArgs e)
    {
        var version = ApplicationVersionService.GetDisplayVersion();

        MessageBox.Show(
            this,
            $"RealTech iAWS - Automatic Weighing System\n" +
            $"Version v{version}\n\n" +
            "Single Weighbridge • First RFID Wins • 4 Editable Cameras\n" +
            "RealTech Systems",
            "About RealTech iAWS",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    protected override void OnClosed(EventArgs e)
    {
        Loaded -= OnLoaded;
        _cameraStreams.Stop();

        Camera1VideoView.MediaPlayer = null;
        Camera2VideoView.MediaPlayer = null;
        Camera3VideoView.MediaPlayer = null;
        Camera4VideoView.MediaPlayer = null;
        Camera1VideoView.Dispose();
        Camera2VideoView.Dispose();
        Camera3VideoView.Dispose();
        Camera4VideoView.Dispose();

        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
