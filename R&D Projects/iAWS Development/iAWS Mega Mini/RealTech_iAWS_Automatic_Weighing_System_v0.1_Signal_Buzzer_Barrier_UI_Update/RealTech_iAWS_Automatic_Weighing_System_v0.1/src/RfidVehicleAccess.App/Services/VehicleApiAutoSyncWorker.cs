using Microsoft.Extensions.Hosting;

namespace RfidVehicleAccess.Services;

public sealed class VehicleApiAutoSyncWorker(
    AppOptions options,
    VehicleApiImportService importService,
    AppLogger logger,
    SystemEventHub eventHub) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await logger.StatusAsync(
            options.Import.ApiAutoSyncEnabled
                ? $"Vehicle API auto sync started. Interval: " +
                  $"{Math.Max(1, options.Import.ApiAutoSyncIntervalSeconds)} second(s)."
                : "Vehicle API auto sync is disabled.",
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Import.ApiAutoSyncEnabled)
                {
                    var result = await importService.ImportAllAsync(stoppingToken);
                    eventHub.PublishCountersChanged();

                    await logger.StatusAsync(
                        $"Vehicle API auto sync cycle completed. " +
                        $"Sources: {result.Sources.Count}; successful: " +
                        $"{result.SuccessfulSourceCount}; failed: {result.FailedSourceCount}.",
                        stoppingToken);
                }

                var intervalSeconds = Math.Max(
                    1,
                    options.Import.ApiAutoSyncIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await logger.StatusAsync(
                    $"Vehicle API auto sync cycle failed: {ex.Message}",
                    CancellationToken.None);

                var retrySeconds = Math.Max(
                    5,
                    options.Import.ApiAutoSyncIntervalSeconds);
                await Task.Delay(TimeSpan.FromSeconds(retrySeconds), stoppingToken);
            }
        }
    }
}
