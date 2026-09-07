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
                "Loading application settings",
                "Reading the protected device, hardware, camera and server configuration.",
                10);

            var options = LoadOptions();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Preparing local storage",
                "Checking the database, image folders and log folders.",
                25);

            PathResolver.EnsureCaptureStorage();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Preparing application services",
                "Creating RFID, camera, hardware, MQTT, SFTP/FTP and user-interface services.",
                40);

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(options);
                    services.AddSingleton<SystemEventHub>();
                    services.AddSingleton<AppLogger>();
                    services.AddSingleton<AppConfigurationService>();
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
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Initializing local database",
                "Creating or upgrading the vehicle, transaction and recharge tables.",
                58);

            var initializer = _host.Services.GetRequiredService<DatabaseInitializer>();
            await initializer.InitializeAsync();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Loading the dashboard",
                "Preparing vehicle counters, status panels and operator controls.",
                72);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            WindowsBrandingService.ApplyTo(mainWindow);

            await ReportStartupProgressAsync(
                loadingWindow,
                "Starting background connections",
                "Starting hardware, internet, vehicle API, server, MQTT recharge and pending-data synchronization.",
                86);

            await _host.StartAsync();

            await ReportStartupProgressAsync(
                loadingWindow,
                "Opening RealTech iTOLL Automation",
                "Startup is complete. Camera streams and live status indicators are opening now.",
                100);

            mainWindow.Show();
            loadingWindow.Close();
        }
        catch (Exception ex)
        {
            loadingWindow.UpdateProgress(
                "Startup failed",
                "The application could not finish loading. Review the error details shown next.",
                100);

            MessageBox.Show(
                ex.ToString(),
                "RealTech iTOLL Automation - Startup Error",
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

        ApplyHardwareCompatibilityDefaults(options.Hardware);
        ApplyConnectivityCompatibilityDefaults(options);
        ApplyImportCompatibilityDefaults(options);
        ApplyStorageCleanupCompatibilityDefaults(options);
        ApplyCaptureStorageCompatibilityDefaults(options);
        ApplyExceptionalApprovalCompatibilityDefaults(options);
        ApplyAutoApprovalCompatibilityDefaults(options);
        ApplyFrontendIndicatorCompatibilityDefaults(options);
        ApplyPaymentCompatibilityDefaults(options);
        ApplySecurityCompatibilityDefaults(options);
        ApplyMqttTopicCompatibilityDefaults(options);
        return options;
    }

    private static void ApplyExceptionalApprovalCompatibilityDefaults(AppOptions options)
    {
        options.ExceptionalApproval ??= new ExceptionalApprovalOptions();
    }

    private static void ApplyAutoApprovalCompatibilityDefaults(AppOptions options)
    {
        options.AutoApproval ??= new AutoApprovalOptions();
    }

    private static void ApplyFrontendIndicatorCompatibilityDefaults(AppOptions options)
    {
        options.FrontendIndicators ??= new FrontendIndicatorOptions();
    }

    private static void ApplyConnectivityCompatibilityDefaults(AppOptions options)
    {
        options.Connectivity ??= new ConnectivityOptions();
        options.Connectivity.CheckEndpoints ??= new ConnectivityOptions().CheckEndpoints;
    }

    private static void ApplyImportCompatibilityDefaults(AppOptions options)
    {
        options.Import ??= new ImportOptions();
        var defaults = new ImportOptions();

        if (string.IsNullOrWhiteSpace(options.Import.ApiEndpoint))
        {
            options.Import.ApiEndpoint = defaults.ApiEndpoint;
        }

        if (string.IsNullOrWhiteSpace(options.Import.ApiUsername))
        {
            options.Import.ApiUsername = defaults.ApiUsername;
        }

        if (options.Import.ApiRequestTimeoutSeconds <= 0)
        {
            options.Import.ApiRequestTimeoutSeconds = defaults.ApiRequestTimeoutSeconds;
        }

        if (options.Import.ApiAutoSyncIntervalSeconds <= 0)
        {
            options.Import.ApiAutoSyncIntervalSeconds = defaults.ApiAutoSyncIntervalSeconds;
        }

        options.Import.ApiSources ??= [];
        options.Import.ApiSources = options.Import.ApiSources
            .Where(source => source is not null)
            .Select((source, index) => new VehicleApiSourceOptions
            {
                Name = string.IsNullOrWhiteSpace(source.Name)
                    ? $"Vehicle CSV API {index + 1}"
                    : source.Name.Trim(),
                Enabled = source.Enabled,
                Priority = source.Priority <= 0 ? index + 1 : source.Priority,
                Endpoint = source.Endpoint?.Trim() ?? string.Empty,
                Username = source.Username?.Trim() ?? string.Empty,
                RequestTimeoutSeconds = source.RequestTimeoutSeconds <= 0
                    ? defaults.ApiRequestTimeoutSeconds
                    : Math.Clamp(source.RequestTimeoutSeconds, 1, 300)
            })
            .ToList();

        if (options.Import.ApiSources.Count == 0)
        {
            options.Import.ApiSources.Add(new VehicleApiSourceOptions
            {
                Name = "Primary Vehicle CSV API",
                Enabled = true,
                Priority = 1,
                Endpoint = options.Import.ApiEndpoint,
                Username = options.Import.ApiUsername,
                RequestTimeoutSeconds = options.Import.ApiRequestTimeoutSeconds
            });
        }

        var primarySource = options.Import.ApiSources
            .Where(source => source.Enabled)
            .OrderBy(source => source.Priority)
            .ThenBy(source => source.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? options.Import.ApiSources
                .OrderBy(source => source.Priority)
                .First();

        // Keep legacy settings synchronized for older tools and configuration files.
        options.Import.ApiEndpoint = primarySource.Endpoint;
        options.Import.ApiUsername = primarySource.Username;
        options.Import.ApiRequestTimeoutSeconds = primarySource.RequestTimeoutSeconds;
    }


    private static void ApplyPaymentCompatibilityDefaults(AppOptions options)
    {
        if (options.Payment is null)
        {
            options.Payment = new PaymentOptions
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
            return;
        }

        options.Payment.Categories ??= [];
        options.Payment.Categories = options.Payment.Categories
            .Where(category => category is not null)
            .Select(category => new VehiclePaymentCategoryOptions
            {
                Name = category.Name?.Trim() ?? string.Empty,
                Price = Math.Max(0m, category.Price)
            })
            .Where(category => !string.IsNullOrWhiteSpace(category.Name))
            .GroupBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (options.Payment.Categories.Count == 0)
        {
            options.Payment.Categories.Add(new VehiclePaymentCategoryOptions
            {
                Name = "Default",
                Price = Math.Max(0m, options.Processing.EntryFee)
            });
        }

        var configuredDefault = options.Payment.Categories.FirstOrDefault(category =>
            string.Equals(
                category.Name,
                options.Payment.DefaultCategory?.Trim(),
                StringComparison.OrdinalIgnoreCase));
        configuredDefault ??= options.Payment.Categories[0];
        options.Payment.DefaultCategory = configuredDefault.Name;

        // Keep the legacy value synchronized so old configuration files and tools
        // continue to have a safe fallback price.
        options.Processing.EntryFee = configuredDefault.Price;
    }

    private static void ApplySecurityCompatibilityDefaults(AppOptions options)
    {
        options.Security ??= new SecurityOptions();
        if (string.IsNullOrWhiteSpace(options.Security.AdminControlsPassword))
        {
            options.Security.AdminControlsPassword = "7799";
        }

        if (string.IsNullOrWhiteSpace(options.Security.PaymentConfigurationPassword))
        {
            options.Security.PaymentConfigurationPassword = "7799";
        }

        if (string.IsNullOrWhiteSpace(options.Security.ServerPanelPassword))
        {
            options.Security.ServerPanelPassword = "rts123!@#";
        }
    }

    private static void ApplyMqttTopicCompatibilityDefaults(AppOptions options)
    {
        const string deviceId = "IWS_STP-001";
        var mqtt = options.Server.Mqtt;

        if (string.Equals(
                options.Device.DeviceId,
                "Tambaram iTOLL 001",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                options.Device.DeviceName,
                "STP-IWS-001",
                StringComparison.OrdinalIgnoreCase))
        {
            options.Device.DeviceId = deviceId;
            options.Device.DeviceName = "Tambaram iTOLL 001";
        }

        if (string.Equals(mqtt.ClientId, "STP-IWS-001", StringComparison.OrdinalIgnoreCase))
        {
            mqtt.ClientId = deviceId;
        }

        var configuredDeviceId = string.IsNullOrWhiteSpace(options.Device.DeviceId)
            ? deviceId
            : options.Device.DeviceId.Trim();
        var transactionTopic = $"{configuredDeviceId}/Device_Response";

        if (IsLegacyTransactionTopic(mqtt.BaseTopic, deviceId))
        {
            mqtt.BaseTopic = transactionTopic;
        }

        if (IsLegacyTransactionTopic(mqtt.PublishTopic, deviceId))
        {
            mqtt.PublishTopic = transactionTopic;
        }

        if (string.Equals(
                mqtt.RechargeSubscribeTopic,
                "STP_RECHARGE/STP-IWS-001",
                StringComparison.OrdinalIgnoreCase))
        {
            mqtt.RechargeSubscribeTopic = $"{deviceId}/STP_RECHARGE_FROM_SERVER";
        }

        if (string.Equals(
                mqtt.RechargeAckTopic,
                "STP_RECHARGE_ACK/STP-IWS-001",
                StringComparison.OrdinalIgnoreCase))
        {
            mqtt.RechargeAckTopic = $"{deviceId}/STP_RECHARGE_ACK_FROM_DEVICE";
        }

        if (string.Equals(
                mqtt.RechargeSubscriberClientId,
                "STP-IWS-001-recharge",
                StringComparison.OrdinalIgnoreCase))
        {
            mqtt.RechargeSubscriberClientId = $"{deviceId}-recharge";
        }
    }

    private static bool IsLegacyTransactionTopic(string? topic, string legacyDeviceId)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return true;
        }

        var normalized = topic.Trim().Trim('/');
        return normalized.Equals("STP_COM", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("/STP_COM", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals($"{legacyDeviceId}/STP_COM", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Device_Response", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyStorageCleanupCompatibilityDefaults(AppOptions options)
    {
        options.Storage ??= new StorageOptions();

        if (options.Storage.ImageRetentionDays <= 0)
        {
            options.Storage.ImageRetentionDays = 30;
        }

        if (options.Storage.LogRetentionDays <= 0)
        {
            options.Storage.LogRetentionDays = 30;
        }
    }

    private static void ApplyCaptureStorageCompatibilityDefaults(AppOptions options)
    {
        if (IsLegacyRelativePath(options.Storage.DatabaseFile, "Data\\vehicle-access.db"))
        {
            options.Storage.DatabaseFile = "%REALTECH_SYSTEMS%\\Data\\vehicle-access.db";
        }

        if (IsLegacyRelativePath(options.Cameras.LocalImageFolder, "Data\\Images"))
        {
            options.Cameras.LocalImageFolder = "%REALTECH_SYSTEMS%\\Images";
        }

        if (IsLegacyRelativePath(options.Storage.StatusLogFile, "Logs\\status.log"))
        {
            options.Storage.StatusLogFile = "%REALTECH_SYSTEMS%\\Logs\\Status\\status.log";
        }

        if (IsLegacyRelativePath(options.Storage.ServerLogFile, "Logs\\server.log"))
        {
            options.Storage.ServerLogFile = "%REALTECH_SYSTEMS%\\Logs\\Server\\server.log";
        }
    }

    private static bool IsLegacyRelativePath(string? configuredPath, string legacyPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        return string.Equals(
            configuredPath.Trim().Replace('/', '\\').TrimStart('.', '\\'),
            legacyPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyHardwareCompatibilityDefaults(HardwareOptions hardware)
    {
        // v0.1.9 configurations do not contain ReadMode. The deployed UHF readers
        // use binary CF frames, while the control unit remains newline-delimited.
        hardware.InRfid.ReadMode = string.IsNullOrWhiteSpace(hardware.InRfid.ReadMode)
            ? "UhfCfFrame"
            : hardware.InRfid.ReadMode;
        hardware.OutRfid.ReadMode = string.IsNullOrWhiteSpace(hardware.OutRfid.ReadMode)
            ? "UhfCfFrame"
            : hardware.OutRfid.ReadMode;
        hardware.Control.ReadMode = string.IsNullOrWhiteSpace(hardware.Control.ReadMode)
            ? "LineText"
            : hardware.Control.ReadMode;
        hardware.SensorMessages ??= new SensorMessageOptions();
    }
}
