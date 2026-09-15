using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Data;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

/// <summary>
/// Synchronizes iAWS v0.1 weighing transactions to the configured server.
/// The worker publishes only iAWS weighing, RFID and camera transaction data.
/// Each transaction publishes the single-weighbridge weight and all four camera results.
/// </summary>
public sealed class ServerSyncWorker(
    AppOptions options,
    TripRepository tripRepository,
    MqttPublishService mqttPublisher,
    HttpApiPublishService apiPublisher,
    ImageUploadService imageUploader,
    TripSyncCoordinator tripSyncCoordinator,
    AppLogger logger,
    SystemEventHub eventHub) : BackgroundService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string? _lastIdleMessage;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Server.Enabled)
        {
            await logger.ServerAsync(
                "iAWS server synchronization is disabled. Local weighing remains active.",
                stoppingToken);
        }
        else if (!HasEnabledTransactionMethod())
        {
            await logger.ServerAsync(
                "iAWS server synchronization is enabled, but both MQTT and HTTP API methods are disabled. Pending transactions will remain local.",
                stoppingToken);
        }
        else
        {
            await logger.ServerAsync(
                $"iAWS server synchronization started | methods={DescribeEnabledTransactionMethods()} | " +
                $"interval={Math.Max(1, options.Server.SyncIntervalSeconds)}s | " +
                $"batch={Math.Max(1, options.Server.MaxBatchSize)} | " +
                $"images={(imageUploader.IsEnabled ? imageUploader.ProtocolName : "disabled")}.",
                stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Server.Enabled && HasEnabledTransactionMethod())
                {
                    await tripSyncCoordinator.RunAsync(
                        () => SynchronizePendingTripsAsync(stoppingToken),
                        stoppingToken);
                }

                await DelayUntilNextCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await logger.ServerAsync(
                    $"iAWS synchronization worker error: {GetUsefulErrorMessage(ex)}",
                    CancellationToken.None);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task SynchronizePendingTripsAsync(CancellationToken cancellationToken)
    {
        var cycleTimer = Stopwatch.StartNew();
        var batchSize = Math.Max(1, options.Server.MaxBatchSize);
        var pendingTrips = await tripRepository.GetPendingAsync(batchSize, cancellationToken);
        var totalPending = await tripRepository.CountPendingAsync(cancellationToken);

        if (pendingTrips.Count == 0)
        {
            if (totalPending == 0)
            {
                await LogIdleStateOnceAsync("No pending iAWS transactions.", cancellationToken);
            }
            else
            {
                var nextRetryAt = await tripRepository.GetNextPendingRetryAtAsync(cancellationToken);
                var retryText = nextRetryAt.HasValue
                    ? $" Next retry: {nextRetryAt.Value:dd-MM-yy HH:mm:ss}."
                    : string.Empty;
                await LogIdleStateOnceAsync(
                    $"{totalPending} pending iAWS transaction(s) are waiting for retry.{retryText}",
                    cancellationToken);
            }

            eventHub.PublishCountersChanged();
            return;
        }

        _lastIdleMessage = null;
        await logger.ServerAsync(
            $"iAWS SYNC START | eligible={pendingTrips.Count} | pending={totalPending} | batch={batchSize}.",
            cancellationToken);

        var synchronizedCount = 0;
        var failedCount = 0;
        foreach (var trip in pendingTrips)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await SynchronizeTripAsync(trip, cancellationToken))
            {
                synchronizedCount++;
            }
            else
            {
                failedCount++;
            }
        }

        eventHub.PublishCountersChanged();
        var remaining = await tripRepository.CountPendingAsync(cancellationToken);
        cycleTimer.Stop();
        await logger.ServerAsync(
            $"iAWS SYNC COMPLETE | synced={synchronizedCount} | failed={failedCount} | " +
            $"remaining={remaining} | elapsed={FormatElapsed(cycleTimer.Elapsed)}.",
            cancellationToken);
    }

    private async Task<bool> SynchronizeTripAsync(
        TripRecord trip,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();
        var attemptAt = DateTimeOffset.Now;
        var attemptCount = trip.SyncAttemptCount + 1;
        var stage = "initialization";
        var prefix = $"TXN {trip.Id}";

        try
        {
            await logger.ServerAsync(
                $"{prefix} START | {(trip.Direction == LaneDirection.In ? "IN" : "OUT")} | " +
                $"RFID={trip.RfidNumber} | weight={trip.WeightKg:0.###}kg | attempt={attemptCount}.",
                cancellationToken);

            stage = "camera upload";
            await UploadFourCameraImagesAsync(trip, cancellationToken);

            if (options.Server.Mqtt.Enabled)
            {
                stage = "MQTT payload serialization";
                var mqttPayload = BuildPayload(trip);
                var mqttPayloadBytes = Encoding.UTF8.GetByteCount(mqttPayload);

                stage = "MQTT publish";
                var topic = ResolvePublishTopic();
                await logger.ServerAsync(
                    $"{prefix} MQTT PUBLISH | topic={topic} | bytes={mqttPayloadBytes}.",
                    cancellationToken);
                await mqttPublisher.PublishAsync(topic, mqttPayload, cancellationToken);
                await logger.ServerAsync(
                    $"{prefix} MQTT SERVER RESPONSE | connection accepted; publish completed | qos={Math.Clamp(options.Server.Mqtt.QualityOfService, 0, 1)}.",
                    cancellationToken);
            }

            if (options.Server.Api.Enabled)
            {
                stage = "HTTP API payload serialization";
                var apiPayload = BuildApiPayload(trip);
                var apiPayloadBytes = Encoding.UTF8.GetByteCount(apiPayload);

                stage = "HTTP API publish";
                await logger.ServerAsync(
                    $"{prefix} API POST | endpoint={options.Server.Api.Endpoint} | bytes={apiPayloadBytes}.",
                    cancellationToken);
                var apiResponse = await apiPublisher.PublishAsync(apiPayload, cancellationToken);
                await logger.ServerAsync(
                    $"{prefix} API SERVER RESPONSE | bytes={Encoding.UTF8.GetByteCount(apiResponse)} | " +
                    $"body={FormatServerResponse(apiResponse)}.",
                    cancellationToken);
            }

            stage = "local sync-state update";
            await tripRepository.MarkSyncedAsync(trip.Id, attemptCount, attemptAt, cancellationToken);

            timer.Stop();
            await logger.ServerAsync(
                $"{prefix} COMPLETE | direction={(trip.Direction == LaneDirection.In ? "IN" : "OUT")} | " +
                $"weight={trip.WeightKg:0.###}kg | elapsed={FormatElapsed(timer.Elapsed)}.",
                cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            timer.Stop();
            var detailedError = GetDetailedErrorMessage(ex, 1800);
            var error = $"{stage}: {detailedError}";
            var retryDelay = CalculateRetryDelay(attemptCount);
            var nextAttemptAt = DateTimeOffset.Now.Add(retryDelay);

            try
            {
                await tripRepository.MarkSyncFailureAsync(
                    trip.Id,
                    attemptCount,
                    attemptAt,
                    nextAttemptAt,
                    error,
                    cancellationToken);
            }
            catch (Exception persistenceException) when (persistenceException is not OperationCanceledException)
            {
                await logger.ServerAsync(
                    $"{prefix} retry-state save failed: {GetUsefulErrorMessage(persistenceException)}",
                    cancellationToken);
            }

            // This is the newest row in the on-screen SERVER LOG, so keep the
            // detailed protocol/server response here too. It prevents the useful
            // CAMx SERVER RESPONSE row from being hidden below a generic multiline
            // WinSCP exception in the small log viewport.
            await logger.ServerAsync(
                $"{prefix} FAILED | stage={stage} | error={detailedError} | " +
                $"retry={nextAttemptAt:dd-MM-yy HH:mm:ss}.",
                cancellationToken);

            // Put the actual transfer response AFTER the generic transaction failure.
            // ServerLogs inserts newest rows at index 0, so these two compact rows stay
            // visible at the top of the small SERVER LOG panel instead of being hidden
            // underneath the transaction failure row.
            var transferError = FindImageTransferException(ex);
            if (transferError is not null)
            {
                await logger.ServerAsync(
                    $"{prefix} {transferError.Protocol} SERVER REPLY | " +
                    $"{FormatServerResponse(transferError.ServerReply, 850)}",
                    cancellationToken);
                await logger.ServerAsync(
                    $"{prefix} {transferError.Protocol} SERVER/PROTOCOL RESPONSE | " +
                    $"{FormatServerResponse(transferError.Diagnostics, 850)} | " +
                    $"retry={nextAttemptAt:dd-MM-yy HH:mm:ss}.",
                    cancellationToken);
            }

            return false;
        }
    }

    private async Task UploadFourCameraImagesAsync(
        TripRecord trip,
        CancellationToken cancellationToken)
    {
        if (!imageUploader.IsEnabled)
        {
            await logger.ServerAsync(
                $"TXN {trip.Id} CAMERA UPLOAD SKIPPED | transfer is disabled; local paths will still be published.",
                cancellationToken);
            return;
        }

        var changed = false;
        for (var cameraNumber = 1; cameraNumber <= 4; cameraNumber++)
        {
            var localPath = GetCameraLocalPath(trip, cameraNumber);
            var remotePath = GetCameraRemotePath(trip, cameraNumber);

            if (!string.IsNullOrWhiteSpace(remotePath))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                await logger.ServerAsync(
                    $"TXN {trip.Id} CAM{cameraNumber} UPLOAD SKIPPED | local image not available.",
                    cancellationToken);
                continue;
            }

            var fileInfo = new FileInfo(localPath);
            var upload = options.Server.ImageUpload;
            var protocol = imageUploader.ProtocolName;
            await logger.ServerAsync(
                $"TXN {trip.Id} CAM{cameraNumber} {protocol} CONNECT | " +
                $"host={upload.Host} | port={upload.Port} | user={upload.Username} | " +
                $"security={DescribeImageUploadSecurity(upload)} | remote-dir={NormalizeLogValue(upload.RemoteDirectory)} | " +
                $"local={fileInfo.Name} | bytes={fileInfo.Length}.",
                cancellationToken);

            ImageUploadResult? result;
            try
            {
                result = await imageUploader.UploadFileAsync(localPath, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Keep the protocol/server diagnostic visible in the main SERVER LOG.
                // Use the full exception chain instead of ex.Message only, because the
                // outer ImageUploadService exception contains the useful WinSCP/SFTP
                // server reply while the inner exception is often only the generic
                // "Peer certificate rejected" message.
                await logger.ServerAsync(
                    $"TXN {trip.Id} CAM{cameraNumber} {protocol} SERVER RESPONSE | FAILED | " +
                    $"{GetDetailedErrorMessage(ex, 1800)}",
                    cancellationToken);
                throw;
            }

            if (result is null)
            {
                await logger.ServerAsync(
                    $"TXN {trip.Id} CAM{cameraNumber} {protocol} SERVER RESPONSE | no upload result returned.",
                    cancellationToken);
                continue;
            }

            await logger.ServerAsync(
                $"TXN {trip.Id} CAM{cameraNumber} {result.Protocol} SERVER RESPONSE | " +
                $"{FormatServerResponse(result.ServerResponse, 1600)}",
                cancellationToken);

            var uploadedAt = DateTimeOffset.Now;
            SetCameraRemoteData(trip, cameraNumber, result.RemotePath, uploadedAt);
            changed = true;
            await logger.ServerAsync(
                $"TXN {trip.Id} CAM{cameraNumber} UPLOADED | protocol={result.Protocol} | " +
                $"remote={result.RemotePath} | bytes={result.BytesUploaded} | " +
                $"elapsed={FormatElapsed(result.Duration)} | connection={(result.ReusedConnection ? "reused" : "new")}.",
                cancellationToken);
        }

        if (changed)
        {
            // Camera 1 continues to mirror the old single-image fields only for
            // backwards database compatibility. The iAWS payload contains Camera 1-4.
            trip.RemoteImagePath = trip.Camera1RemoteImagePath;
            trip.ImageUploadedAt = trip.Camera1ImageUploadedAt;
            await tripRepository.UpdateIawsDataAsync(trip, cancellationToken);

            if (!string.IsNullOrWhiteSpace(trip.Camera1RemoteImagePath) &&
                trip.Camera1ImageUploadedAt.HasValue)
            {
                await tripRepository.MarkImageUploadedAsync(
                    trip.Id,
                    trip.Camera1RemoteImagePath,
                    trip.Camera1ImageUploadedAt.Value,
                    cancellationToken);
            }
        }
    }

    private string BuildPayload(TripRecord trip)
    {
        var payload = new
        {
            schemaVersion = "iAWS-v0.1",
            system = "RealTech iAWS Automatic Weighing System",
            transactionId = trip.Id,
            siteId = trip.SiteId,
            deviceId = trip.DeviceId,
            laneId = options.Device.LaneId,
            deviceName = options.Device.DeviceName,
            direction = trip.Direction == LaneDirection.In ? "IN" : "OUT",
            rfidNumber = trip.RfidNumber,
            vehicleNumber = trip.VehicleNumber,
            vehicleCategory = trip.VehicleCategory,
            weight = new
            {
                valueKg = trip.WeightKg,
                targetKg = trip.TargetWeightKg,
                targetReached = trip.WeightKg >= trip.TargetWeightKg
            },
            cameras = Enumerable.Range(1, 4).Select(cameraNumber => new
            {
                cameraNumber,
                captureStatus = GetCameraStatus(trip, cameraNumber),
                localFileName = GetFileNameOrNull(GetCameraLocalPath(trip, cameraNumber)),
                remotePath = NullIfEmpty(GetCameraRemotePath(trip, cameraNumber)),
                uploadedAt = GetCameraUploadedAt(trip, cameraNumber)
            }).ToArray(),
            tripDate = trip.ProcessedAt.ToString("yyyy-MM-dd"),
            tripTime = trip.ProcessedAt.ToString("HH:mm:ss"),
            processedAt = trip.ProcessedAt,
            notes = NullIfEmpty(trip.Notes)
        };

        return JsonSerializer.Serialize(payload, _jsonOptions);
    }

    private string BuildApiPayload(TripRecord trip)
    {
        var configuredMaterialType = options.Server.Api.DefaultMaterialType?.Trim();
        var materialType = !string.IsNullOrWhiteSpace(configuredMaterialType)
            ? configuredMaterialType
            : trip.VehicleCategory;

        var payload = new
        {
            rf_id = trip.RfidNumber,
            action = string.IsNullOrWhiteSpace(options.Server.Api.Action)
                ? "WEIGH"
                : options.Server.Api.Action.Trim(),
            weight = trip.WeightKg.ToString("0.###", CultureInfo.InvariantCulture),
            front = GetPreferredCameraFileName(trip, 1),
            back = GetPreferredCameraFileName(trip, 2),
            left = GetPreferredCameraFileName(trip, 3),
            right = GetPreferredCameraFileName(trip, 4),
            dates = trip.ProcessedAt.LocalDateTime.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture),
            material_type = materialType ?? string.Empty
        };

        return JsonSerializer.Serialize(payload, _jsonOptions);
    }

    private static string GetPreferredCameraFileName(TripRecord trip, int cameraNumber)
    {
        var remotePath = GetCameraRemotePath(trip, cameraNumber);
        if (!string.IsNullOrWhiteSpace(remotePath))
        {
            return Path.GetFileName(remotePath.Replace('\\', '/'));
        }

        var localPath = GetCameraLocalPath(trip, cameraNumber);
        return string.IsNullOrWhiteSpace(localPath)
            ? string.Empty
            : Path.GetFileName(localPath);
    }

    private async Task LogIdleStateOnceAsync(string message, CancellationToken cancellationToken)
    {
        if (string.Equals(_lastIdleMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastIdleMessage = message;
        await logger.ServerAsync(message, cancellationToken);
    }

    private async Task DelayUntilNextCycleAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(
            TimeSpan.FromSeconds(Math.Max(1, options.Server.SyncIntervalSeconds)),
            cancellationToken);
    }

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        var initial = Math.Max(1, options.Server.InitialRetryDelaySeconds);
        var maximum = Math.Max(initial, options.Server.MaxRetryDelaySeconds);
        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var seconds = Math.Min(maximum, initial * Math.Pow(2, exponent));
        return TimeSpan.FromSeconds(seconds);
    }

    private bool HasEnabledTransactionMethod() =>
        options.Server.Mqtt.Enabled || options.Server.Api.Enabled;

    private string DescribeEnabledTransactionMethods()
    {
        var methods = new List<string>(2);
        if (options.Server.Mqtt.Enabled)
        {
            methods.Add($"MQTT({ResolvePublishTopic()})");
        }

        if (options.Server.Api.Enabled)
        {
            methods.Add($"API({options.Server.Api.Endpoint})");
        }

        return methods.Count == 0 ? "disabled" : string.Join(" + ", methods);
    }

    private string ResolvePublishTopic()
    {
        if (!string.IsNullOrWhiteSpace(options.Server.Mqtt.PublishTopic))
        {
            return options.Server.Mqtt.PublishTopic.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Server.Mqtt.BaseTopic))
        {
            return options.Server.Mqtt.BaseTopic.Trim();
        }

        return $"{options.Device.DeviceId}/Device_Response";
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? GetFileNameOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);

    private static string GetCameraLocalPath(TripRecord trip, int cameraNumber) => cameraNumber switch
    {
        1 => trip.Camera1ImagePath,
        2 => trip.Camera2ImagePath,
        3 => trip.Camera3ImagePath,
        4 => trip.Camera4ImagePath,
        _ => string.Empty
    };

    private static string GetCameraStatus(TripRecord trip, int cameraNumber) => cameraNumber switch
    {
        1 => trip.Camera1ImageCaptureStatus,
        2 => trip.Camera2ImageCaptureStatus,
        3 => trip.Camera3ImageCaptureStatus,
        4 => trip.Camera4ImageCaptureStatus,
        _ => string.Empty
    };

    private static string GetCameraRemotePath(TripRecord trip, int cameraNumber) => cameraNumber switch
    {
        1 => trip.Camera1RemoteImagePath,
        2 => trip.Camera2RemoteImagePath,
        3 => trip.Camera3RemoteImagePath,
        4 => trip.Camera4RemoteImagePath,
        _ => string.Empty
    };

    private static DateTimeOffset? GetCameraUploadedAt(TripRecord trip, int cameraNumber) => cameraNumber switch
    {
        1 => trip.Camera1ImageUploadedAt,
        2 => trip.Camera2ImageUploadedAt,
        3 => trip.Camera3ImageUploadedAt,
        4 => trip.Camera4ImageUploadedAt,
        _ => null
    };

    private static void SetCameraRemoteData(
        TripRecord trip,
        int cameraNumber,
        string remotePath,
        DateTimeOffset uploadedAt)
    {
        switch (cameraNumber)
        {
            case 1:
                trip.Camera1RemoteImagePath = remotePath;
                trip.Camera1ImageUploadedAt = uploadedAt;
                break;
            case 2:
                trip.Camera2RemoteImagePath = remotePath;
                trip.Camera2ImageUploadedAt = uploadedAt;
                break;
            case 3:
                trip.Camera3RemoteImagePath = remotePath;
                trip.Camera3ImageUploadedAt = uploadedAt;
                break;
            case 4:
                trip.Camera4RemoteImagePath = remotePath;
                trip.Camera4ImageUploadedAt = uploadedAt;
                break;
        }
    }

    private static string DescribeImageUploadSecurity(ImageUploadOptions upload)
    {
        if (ImageUploadService.IsSftpMode(upload.Mode))
        {
            return "SSH/SFTP";
        }

        if (!ImageUploadService.IsFtpMode(upload.Mode))
        {
            return "disabled";
        }

        if (!upload.UseTls)
        {
            return "plain FTP";
        }

        if (ImageUploadService.IsValidSha256CertificateFingerprint(
                upload.TlsCertificateSha256Fingerprint))
        {
            return "Explicit FTPS + pinned SHA-256 certificate";
        }

        return upload.AllowInvalidTlsCertificate
            ? "Explicit FTPS + accept-any-certificate (UNSAFE)"
            : "Explicit FTPS + Windows certificate validation";
    }

    private static string NormalizeLogValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "<empty>" : FormatServerResponse(value, 500);

    private static string FormatServerResponse(string? value, int maxLength = 1000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        var singleLine = string.Join(
            " ",
            value.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (singleLine.Length <= maxLength)
        {
            return singleLine;
        }

        return singleLine[..maxLength] + "...[truncated]";
    }

    private static ImageTransferException? FindImageTransferException(Exception exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is ImageTransferException transferException)
            {
                return transferException;
            }

            current = current.InnerException;
        }

        return null;
    }

    private static string GetUsefulErrorMessage(Exception exception) =>
        GetDetailedErrorMessage(exception, 1000);

    private static string GetDetailedErrorMessage(Exception exception, int maxLength)
    {
        // Preserve the OUTER exception first. ImageUploadService intentionally wraps
        // WinSCP/SSH.NET failures with protocol-specific server diagnostics. The old
        // implementation walked to the innermost exception and discarded that useful
        // information, which is why the UI showed only "Peer certificate rejected".
        var parts = new List<string>();
        Exception? current = exception;
        while (current is not null)
        {
            var message = string.IsNullOrWhiteSpace(current.Message)
                ? current.GetType().Name
                : FormatServerResponse(current.Message, maxLength);

            if (!parts.Any(existing =>
                    string.Equals(existing, message, StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add(message);
            }

            current = current.InnerException;
        }

        var combined = string.Join(" | caused-by: ", parts);
        return combined.Length <= maxLength
            ? combined
            : combined[..maxLength] + "...[truncated]";
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalSeconds >= 1
            ? $"{elapsed.TotalSeconds:0.00}s"
            : $"{elapsed.TotalMilliseconds:0}ms";
}
