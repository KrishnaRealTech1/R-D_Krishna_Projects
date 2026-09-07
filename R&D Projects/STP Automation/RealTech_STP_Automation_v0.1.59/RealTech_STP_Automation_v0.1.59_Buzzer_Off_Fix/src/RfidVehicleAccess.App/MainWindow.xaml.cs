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
    private readonly AppConfigurationService _configurationService;
    private readonly StorageCleanupWorker _storageCleanup;

    public MainWindow(
        MainViewModel viewModel,
        CameraStreamService cameraStreams,
        AppOptions options,
        HardwareGateway hardwareGateway,
        AppConfigurationService configurationService,
        StorageCleanupWorker storageCleanup)
    {
        InitializeComponent();
        AboutVersionMenuItem.Header =
            $"RealTech STP Automation v{ApplicationVersionService.GetDisplayVersion()}";
        _viewModel = viewModel;
        _cameraStreams = cameraStreams;
        _options = options;
        _hardwareGateway = hardwareGateway;
        _configurationService = configurationService;
        _storageCleanup = storageCleanup;
        DataContext = viewModel;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InVideoView.MediaPlayer = _cameraStreams.InMediaPlayer;
        OutVideoView.MediaPlayer = _cameraStreams.OutMediaPlayer;
        _cameraStreams.Start();
    }

    private void OpenHardwareSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HardwareSettingsWindow(
            _options,
            _hardwareGateway,
            _configurationService)
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

        var dialog = new AdminControlsWindow(_viewModel)
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
            _hardwareGateway,
            _cameraStreams,
            _viewModel,
            _storageCleanup)
        {
            Owner = this
        };

        dialog.ShowDialog();
    }

    protected override void OnClosed(EventArgs e)
    {
        Loaded -= OnLoaded;
        _cameraStreams.Stop();
        InVideoView.MediaPlayer = null;
        OutVideoView.MediaPlayer = null;
        InVideoView.Dispose();
        OutVideoView.Dispose();
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
