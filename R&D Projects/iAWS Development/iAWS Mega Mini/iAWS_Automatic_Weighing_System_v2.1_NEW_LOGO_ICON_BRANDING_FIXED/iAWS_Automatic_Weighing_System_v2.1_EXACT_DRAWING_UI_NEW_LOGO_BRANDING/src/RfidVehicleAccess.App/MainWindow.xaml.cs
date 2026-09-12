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

    public MainWindow(
        IawsMainViewModel viewModel,
        IawsCameraStreamService cameraStreams,
        AppOptions options,
        IawsHardwareGateway hardware,
        AppConfigurationService configurationService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _cameraStreams = cameraStreams;
        _options = options;
        _hardware = hardware;
        _configurationService = configurationService;
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
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
