using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Data;
using RfidVehicleAccess.Services;
using RfidVehicleAccess.ViewModels;

namespace RfidVehicleAccess;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        WindowsBrandingService.Initialize();

        var loadingWindow = new StartupLoadingWindow();
        MainWindow = loadingWindow;
        WindowsBrandingService.ApplyTo(loadingWindow);
        loadingWindow.Show();

        try
        {
            await ReportStartupProgressAsync(
                loadingWindow,
                "Loading iAWS configuration",
                "Reading weighing, RFID, camera, server, payment and connectivity settings.",
                10);

            var options = LoadOptions();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Preparing local storage",
                "Checking iAWS database, image folders and status/server/API logs.",
                22);

            PathResolver.EnsureCaptureStorage();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Preparing application services",
                "Creating the iAWS weighing workflow and the full iTOLL production backend services.",
                38);

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(options);
                    services.AddSingleton<SystemEventHub>();
                    services.AddSingleton<AppLogger>();
                    services.AddSingleton<AppConfigurationService>();

                    // iTOLL production data / payment / synchronization backend.
                    services.AddSingleton<RfidValidator>();
                    services.AddSingleton<PaymentPriceResolver>();
                    services.AddSingleton<BalanceUpdateCoordinator>();
                    services.AddSingleton<TripSyncCoordinator>();
                    services.AddSingleton<StorageCleanupWorker>();

                    services.AddSingleton<SqliteConnectionFactory>();
                    services.AddSingleton<DatabaseInitializer>();
                    services.AddSingleton<VehicleRepository>();
                    services.AddSingleton<ContractorRepository>();
                    services.AddSingleton<TripRepository>();
                    services.AddSingleton<ImportAuditRepository>();
                    services.AddSingleton<RechargeRepository>();

                    services.AddSingleton<VehicleImportService>();
                    services.AddSingleton<VehicleApiImportService>();
                    services.AddSingleton<CameraStreamService>();
                    services.AddSingleton<CameraSnapshotService>();
                    services.AddSingleton<IExceptionalApprovalService, ExceptionalApprovalService>();
                    services.AddSingleton<LaneProcessor>();
                    services.AddSingleton<MqttPublishService>();
                    services.AddSingleton<OnlineTripAuthorizationService>();
                    services.AddSingleton<ImageUploadService>();
                    services.AddSingleton<HardwareGateway>();

                    services.AddHostedService<ApplicationCoordinatorService>();
                    services.AddSingleton<IHostedService>(provider =>
                        provider.GetRequiredService<HardwareGateway>());
                    services.AddHostedService<ServerSyncWorker>();
                    services.AddHostedService<VehicleApiAutoSyncWorker>();
                    services.AddHostedService<MqttRechargeSyncWorker>();
                    services.AddHostedService<InternetConnectivityWorker>();
                    services.AddHostedService<ServerConnectivityWorker>();
                    services.AddSingleton<IHostedService>(provider =>
                        provider.GetRequiredService<StorageCleanupWorker>());

                    services.AddSingleton<MainViewModel>();

                    // Existing iAWS weighing + four-camera workflow.
                    services.AddSingleton<IawsCameraStreamService>();
                    services.AddSingleton<IawsCameraCaptureService>();
                    services.AddSingleton<IawsApiClient>();
                    services.AddSingleton<IawsHardwareGateway>();
                    services.AddSingleton<IawsWeighingProcessor>();
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
                "Initializing local database",
                "Creating or upgrading vehicle, transaction, recharge and payment data tables.",
                52);

            var initializer = _host.Services.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Starting four camera streams",
                "Opening Front, Back, Left and Right iAWS RTSP camera connections.",
                66);

            _host.Services.GetRequiredService<IawsCameraStreamService>().Start();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Starting automation services",
                "Starting weighbridge, RFID, COM hardware, server sync, internet checks and background processing.",
                82);

            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            WindowsBrandingService.ApplyTo(mainWindow);

            await ReportStartupProgressAsync(
                loadingWindow,
                "Opening RealTech iAWS",
                "Automatic Weighing System and iTOLL production functions are ready.",
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
                "RealTech iAWS - Startup Error",
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

        // iAWS compatibility/defaults.
        options.Device ??= new DeviceOptions();
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

        // iTOLL production compatibility/defaults.
        options.Processing ??= new ProcessingOptions();
        options.Processing.AllowedRfidPrefixes ??= ["E2"];
        options.Payment ??= new PaymentOptions
        {
            DefaultCategory = "Default",
            Categories =
            [
                new VehiclePaymentCategoryOptions
                {
                    Name = "Default",
                    Price = Math.Max(0m, options.Processing.EntryFee)
                }
            ]
        };
        options.Payment.Categories ??= [];
        if (options.Payment.Categories.Count == 0)
        {
            options.Payment.Categories.Add(new VehiclePaymentCategoryOptions
            {
                Name = "Default",
                Price = Math.Max(0m, options.Processing.EntryFee)
            });
        }

        options.Hardware.InRfid ??= new SerialPortOptions { ReadMode = "UhfCfFrame" };
        options.Hardware.OutRfid ??= new SerialPortOptions { ReadMode = "UhfCfFrame" };
        options.Hardware.Control ??= new SerialPortOptions { ReadMode = "LineText" };
        options.Hardware.BluetoothControl ??= new SerialPortOptions
        {
            PortName = "COM11",
            ReadMode = "LineText",
            BaudRate = 9600
        };
        options.Hardware.WifiControl ??= new WifiControlOptions();
        options.Hardware.SensorMessages ??= new SensorMessageOptions();

        options.Cameras.In ??= new CameraLaneOptions();
        options.Cameras.Out ??= new CameraLaneOptions();

        options.Server ??= new ServerOptions();
        options.Server.Mqtt ??= new MqttOptions();
        options.Server.ImageUpload ??= new ImageUploadOptions();
        options.Connectivity ??= new ConnectivityOptions();
        options.Connectivity.CheckEndpoints ??= new ConnectivityOptions().CheckEndpoints;
        options.Import ??= new ImportOptions();
        options.Import.ApiSources ??= [];
        options.Storage ??= new StorageOptions();
        options.ExceptionalApproval ??= new ExceptionalApprovalOptions();
        options.AutoApproval ??= new AutoApprovalOptions();
        options.FrontendIndicators ??= new FrontendIndicatorOptions();
        options.Security ??= new SecurityOptions();

        if (string.IsNullOrWhiteSpace(options.Security.AdminControlsPassword))
            options.Security.AdminControlsPassword = "7799";
        if (string.IsNullOrWhiteSpace(options.Security.PaymentConfigurationPassword))
            options.Security.PaymentConfigurationPassword = "7799";
        if (string.IsNullOrWhiteSpace(options.Security.ServerPanelPassword))
            options.Security.ServerPanelPassword = "rts123!@#";

        if (options.Import.ApiSources.Count == 0 && !string.IsNullOrWhiteSpace(options.Import.ApiEndpoint))
        {
            options.Import.ApiSources.Add(new VehicleApiSourceOptions
            {
                Name = "Primary Vehicle CSV API",
                Enabled = true,
                Priority = 1,
                Endpoint = options.Import.ApiEndpoint,
                Username = options.Import.ApiUsername,
                RequestTimeoutSeconds = options.Import.ApiRequestTimeoutSeconds <= 0
                    ? 30
                    : options.Import.ApiRequestTimeoutSeconds
            });
        }

        return options;
    }
}
