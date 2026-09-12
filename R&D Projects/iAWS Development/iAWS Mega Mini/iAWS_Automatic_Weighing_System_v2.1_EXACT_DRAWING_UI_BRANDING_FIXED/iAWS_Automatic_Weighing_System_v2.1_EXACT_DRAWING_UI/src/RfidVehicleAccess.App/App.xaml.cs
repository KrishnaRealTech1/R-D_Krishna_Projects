using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Services;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var loadingWindow = new StartupLoadingWindow();
        MainWindow = loadingWindow;
        loadingWindow.Show();

        try
        {
            await ReportStartupProgressAsync(
                loadingWindow,
                "Loading iAWS configuration",
                "Reading weighbridge, RFID, four-camera and REST API settings.",
                12);

            var options = LoadOptions();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Preparing local capture storage",
                "Creating iAWS image and log folders.",
                28);

            PathResolver.EnsureCaptureStorage();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Preparing iAWS services",
                "Creating weighbridge, RFID, four-camera and direct REST API services.",
                48);

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(options);
                    services.AddSingleton<SystemEventHub>();
                    services.AddSingleton<AppLogger>();
                    services.AddSingleton<AppConfigurationService>();

                    services.AddSingleton<IawsCameraStreamService>();
                    services.AddSingleton<IawsCameraCaptureService>();
                    services.AddSingleton<IawsApiClient>();
                    services.AddSingleton<IawsHardwareGateway>();
                    services.AddSingleton<IawsWeighingProcessor>();

                    // Processor subscribes first, then the hardware ports are opened.
                    services.AddSingleton<IHostedService>(provider =>
                        provider.GetRequiredService<IawsWeighingProcessor>());
                    services.AddSingleton<IHostedService>(provider =>
                        provider.GetRequiredService<IawsHardwareGateway>());

                    services.AddSingleton<IawsMainViewModel>();
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Starting four camera streams",
                "Opening Front, Back, Left and Right RTSP camera connections.",
                66);

            _host.Services.GetRequiredService<IawsCameraStreamService>().Start();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Starting weighbridge automation",
                "Weight data is now the process trigger. Waiting for the configured threshold and RFID.",
                82);

            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;

            await ReportStartupProgressAsync(
                loadingWindow,
                "Opening iAWS",
                "Automatic Weighing System is ready.",
                100);

            mainWindow.Show();
            loadingWindow.Close();
        }
        catch (Exception ex)
        {
            loadingWindow.UpdateProgress(
                "Startup failed",
                "iAWS could not finish loading. Review the error details.",
                100);

            MessageBox.Show(
                ex.ToString(),
                "iAWS - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static async Task ReportStartupProgressAsync(
        StartupLoadingWindow loadingWindow,
        string status,
        string detail,
        double percentage)
    {
        loadingWindow.UpdateProgress(status, detail, percentage);
        await loadingWindow.Dispatcher.InvokeAsync(
            static () => { },
            System.Windows.Threading.DispatcherPriority.Render);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private static AppOptions LoadOptions()
    {
        var path = ApplicationPaths.EnsureConfigurationFile();
        var json = File.ReadAllText(path);
        var options = JsonSerializer.Deserialize<AppOptions>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Unable to read appsettings.json.");

        options.Iaws ??= new IawsOptions();
        options.Hardware ??= new HardwareOptions();
        options.Hardware.RfidReader ??= new SerialPortOptions
        {
            PortName = "COM3",
            BaudRate = 115200,
            ReadMode = "UhfCfFrame"
        };
        options.Hardware.WeightBridge ??= new WeightBridgeOptions();
        options.Cameras ??= new CameraOptions();
        options.Cameras.Front ??= new CameraLaneOptions();
        options.Cameras.Back ??= new CameraLaneOptions();
        options.Cameras.Left ??= new CameraLaneOptions();
        options.Cameras.Right ??= new CameraLaneOptions();
        options.Storage ??= new StorageOptions();

        if (string.IsNullOrWhiteSpace(options.Hardware.RfidReader.ReadMode))
            options.Hardware.RfidReader.ReadMode = "UhfCfFrame";
        if (string.IsNullOrWhiteSpace(options.Hardware.WeightBridge.WeightPattern))
            options.Hardware.WeightBridge.WeightPattern = @"[-+]?\d+(?:\.\d+)?";
        if (options.Iaws.ApiTimeoutSeconds <= 0)
            options.Iaws.ApiTimeoutSeconds = 30;
        if (options.Iaws.RfidFreshnessSeconds <= 0)
            options.Iaws.RfidFreshnessSeconds = 30;
        if (options.Iaws.ResetWeightKg < 0)
            options.Iaws.ResetWeightKg = 0;
        if (options.Iaws.TriggerWeightKg < 0)
            options.Iaws.TriggerWeightKg = 0;

        return options;
    }
}
