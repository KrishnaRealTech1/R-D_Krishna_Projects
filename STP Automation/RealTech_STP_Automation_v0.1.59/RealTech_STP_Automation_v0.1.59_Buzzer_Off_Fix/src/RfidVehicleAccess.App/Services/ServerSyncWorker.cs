using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using RfidVehicleAccess.Data;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Services;

public sealed class ServerSyncWorker(
    AppOptions options,
    TripRepository tripRepository,
    VehicleRepository vehicleRepository,
    MqttPublishService mqttPublisher,
    SftpImageUploadService imageUploader,
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
                "Server synchronization is disabled. Local processing remains active.",
                stoppingToken);
        }
        else
        {
            await logger.ServerAsync(
                $"Server synchronization started. MQTT topic: {ResolvePublishTopic()}; " +
                $"cycle interval: {Math.Max(1, options.Server.SyncIntervalSeconds)}s; " +
                $"batch size: {Math.Max(1, options.Server.MaxBatchSize)}; " +
                $"SFTP: {(imageUploader.IsEnabled ? "enabled" : "disabled")}.",
                stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (options.Server.Enabled)
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
                    $"Server synchronization worker error: {GetUsefulErrorMessage(ex)}",
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
                await LogIdleStateOnceAsync("No pending transactions.", cancellationToken);
            }
            else
            {
                var nextRetryAt = await tripRepository.GetNextPendingRetryAtAsync(cancellationToken);
                var retryText = nextRetryAt.HasValue
                    ? $" Next retry: {nextRetryAt.Value:dd-MM-yy HH:mm:ss}."
                    : string.Empty;
                await LogIdleStateOnceAsync(
                    $"{totalPending} pending transaction(s) are waiting for their retry time.{retryText}",
                    cancellationToken);
            }

            eventHub.PublishCountersChanged();
            return;
        }

        _lastIdleMessage = null;
        await logger.ServerAsync(
            $"SYNC CYCLE START | eligible={pendingTrips.Count} | totalPending={totalPending} | " +
            $"batchLimit={batchSize}.",
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
            $"SYNC CYCLE COMPLETE | synced={synchronizedCount} | failed={failedCount} | " +
            $"remaining={remaining} | elapsed={FormatElapsed(cycleTimer.Elapsed)}.",
            cancellationToken);
    }

    private async Task<bool> SynchronizeTripAsync(
        TripRecord trip,
        CancellationToken cancellationToken)
    {
        var transactionTimer = Stopwatch.StartNew();
        var attemptAt = DateTimeOffset.Now;
        var attemptCount = trip.SyncAttemptCount + 1;
        var stage = "initialization";
        var transactionPrefix = $"TXN {trip.Id}";

        await logger.ServerAsync(
            $"{transactionPrefix} START | attempt={attemptCount} | " +
            $"direction={(trip.Direction == LaneDirection.In ? "IN" : "OUT")} | " +
            $"rfid={trip.RfidNumber} | vehicle={trip.VehicleNumber} | " +
            $"processedAt={trip.ProcessedAt:O}.",
            cancellationToken);

        try
        {
            stage = "image upload";
            var remoteImagePath = await UploadImageIfAvailableAsync(trip, cancellationToken);

            stage = "payload serialization";
            var payloadTimer = Stopwatch.StartNew();
            var vehicleCategory = trip.VehicleCategory;
            if (string.IsNullOrWhiteSpace(vehicleCategory))
            {
                var vehicle = await vehicleRepository.GetByRfidAsync(
                    trip.RfidNumber,
                    cancellationToken);
                vehicleCategory = vehicle?.VehicleCategory ?? string.Empty;
            }

            var payload = BuildPayload(trip, remoteImagePath, vehicleCategory);
            var payloadBytes = Encoding.UTF8.GetByteCount(payload);
            payloadTimer.Stop();
            await logger.ServerAsync(
                $"{transactionPrefix} PAYLOAD READY | bytes={payloadBytes} | " +
                $"elapsed={FormatElapsed(payloadTimer.Elapsed)}.",
                cancellationToken);

            stage = "MQTT publish";
            var topic = ResolvePublishTopic();
            var mqttTimer = Stopwatch.StartNew();
            await logger.ServerAsync(
                $"{transactionPrefix} MQTT PUBLISH START | topic={topic} | " +
                $"qos={Math.Clamp(options.Server.Mqtt.QualityOfService, 0, 1)}.",
                cancellationToken);
            await mqttPublisher.PublishAsync(topic, payload, cancellationToken);
            mqttTimer.Stop();
            await logger.ServerAsync(
                $"{transactionPrefix} MQTT ACK RECEIVED | elapsed={FormatElapsed(mqttTimer.Elapsed)}.",
                cancellationToken);

            stage = "local sync-state update";
            await tripRepository.MarkSyncedAsync(
                trip.Id,
                attemptCount,
                attemptAt,
                cancellationToken);

            transactionTimer.Stop();
            await logger.ServerAsync(
                $"{transactionPrefix} COMPLETE | status=Synced | attempt={attemptCount} | " +
                $"image={(string.IsNullOrWhiteSpace(remoteImagePath) ? "not-uploaded" : remoteImagePath)} | " +
                $"totalElapsed={FormatElapsed(transactionTimer.Elapsed)}.",
                cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            transactionTimer.Stop();
            var error = $"{stage}: {GetUsefulErrorMessage(ex)}";
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
            catch (Exception persistenceException) when (
                persistenceException is not OperationCanceledException)
            {
                await logger.ServerAsync(
                    $"{transactionPrefix} RETRY STATE SAVE FAILED | " +
                    $"error={GetUsefulErrorMessage(persistenceException)}.",
                    cancellationToken);
            }

            await logger.ServerAsync(
                $"{transactionPrefix} FAILED | stage={stage} | attempt={attemptCount} | " +
                $"elapsed={FormatElapsed(transactionTimer.Elapsed)} | error={GetUsefulErrorMessage(ex)} | " +
                $"nextRetry={nextAttemptAt:dd-MM-yy HH:mm:ss} ({retryDelay.TotalSeconds:0}s).",
                cancellationToken);
            return false;
        }
    }

    private async Task<string?> UploadImageIfAvailableAsync(
        TripRecord trip,
        CancellationToken cancellationToken)
    {
        var transactionPrefix = $"TXN {trip.Id}";

        if (!imageUploader.IsEnabled)
        {
            await logger.ServerAsync(
                $"{transactionPrefix} IMAGE SKIPPED | SFTP upload is disabled.",
                cancellationToken);
            return null;
        }

        if (!string.IsNullOrWhiteSpace(trip.RemoteImagePath))
        {
            await logger.ServerAsync(
                $"{transactionPrefix} IMAGE REUSED | already uploaded to {trip.RemoteImagePath} | " +
                $"uploadedAt={(trip.ImageUploadedAt.HasValue ? trip.ImageUploadedAt.Value.ToString("O") : "unknown")}.",
                cancellationToken);
            return trip.RemoteImagePath;
        }

        if (string.IsNullOrWhiteSpace(trip.ImagePath) || !File.Exists(trip.ImagePath))
        {
            await logger.ServerAsync(
                $"{transactionPrefix} IMAGE SKIPPED | local image is unavailable | " +
                $"captureStatus={trip.ImageCaptureStatus} | path={trip.ImagePath}.",
                cancellationToken);
            return null;
        }

        var fileInfo = new FileInfo(trip.ImagePath);
        await logger.ServerAsync(
            $"{transactionPrefix} IMAGE UPLOAD START | file={fileInfo.Name} | " +
            $"bytes={fileInfo.Length} ({FormatBytes(fileInfo.Length)}) | path={fileInfo.FullName}.",
            cancellationToken);

        var result = await imageUploader.UploadAsync(trip, cancellationToken)
            ?? throw new IOException("SFTP upload returned no result.");
        var uploadedAt = DateTimeOffset.Now;

        await tripRepository.MarkImageUploadedAsync(
            trip.Id,
            result.RemotePath,
            uploadedAt,
            cancellationToken);
        trip.RemoteImagePath = result.RemotePath;
        trip.ImageUploadedAt = uploadedAt;

        var bytesPerSecond = result.Duration.TotalSeconds <= 0
            ? result.BytesUploaded
            : result.BytesUploaded / result.Duration.TotalSeconds;
        await logger.ServerAsync(
            $"{transactionPrefix} IMAGE UPLOAD COMPLETE | remote={result.RemotePath} | " +
            $"bytes={result.BytesUploaded} | elapsed={FormatElapsed(result.Duration)} | " +
            $"rate={FormatBytes(bytesPerSecond)}/s | " +
            $"connection={(result.ReusedConnection ? "reused" : "new")}.",
            cancellationToken);

        return result.RemotePath;
    }

    private string BuildPayload(
        TripRecord trip,
        string? remoteImagePath,
        string vehicleCategory)
    {
        var payload = new TransactionSyncPayload
        {
            SchemaVersion = "2.0",
            TransactionId = trip.Id,
            SiteId = trip.SiteId,
            DeviceId = trip.DeviceId,
            LaneId = options.Device.LaneId,
            DeviceName = options.Device.DeviceName,
            Direction = trip.Direction == LaneDirection.In ? "IN" : "OUT",
            RfidNumber = trip.RfidNumber,
            VehicleNumber = trip.VehicleNumber,
            VehicleCategory = vehicleCategory,
            AccessType = trip.AccessType.ToString(),
            ContractorId = trip.ContractorCode,
            ContractorName = trip.ContractorName,
            BalanceType = trip.BalanceType,
            AuthorizationSource = trip.AuthorizationSource,
            AuthorizationRequestId = trip.AuthorizationRequestId,
            ServerDecision = trip.ServerDecision,
            ServerReason = trip.ServerReason,
            EntryTransactionId = trip.EntryTripId,
            PreviousBalance = trip.PreviousBalance,
            DebitAmount = trip.DebitAmount,
            NewBalance = trip.NewBalance,
            ImageCaptureStatus = trip.ImageCaptureStatus,
            ImageFileName = string.IsNullOrWhiteSpace(trip.ImagePath)
                ? null
                : Path.GetFileName(trip.ImagePath),
            ImageRemotePath = remoteImagePath,
            TripDate = FormatDate(trip.ProcessedAt),
            TripTime = FormatTime(trip.ProcessedAt),
            TimeZoneOffset = FormatOffset(trip.ProcessedAt),
            ProcessedAt = trip.ProcessedAt,
            ExceptionalApproval = BuildExceptionalApprovalPayload(trip),
            MissingTripNotification = BuildMissingTripNotificationPayload(trip),
            Notes = trip.Notes
        };

        return JsonSerializer.Serialize(payload, _jsonOptions);
    }

    private static MissingTripNotificationSyncPayload? BuildMissingTripNotificationPayload(
        TripRecord trip)
    {
        var reason = trip.ExceptionalApprovalReason;
        if (string.IsNullOrWhiteSpace(reason) &&
            TryReadLegacyExceptionalApproval(trip.Notes, out var legacyApproval))
        {
            reason = legacyApproval.Reason;
        }

        string? missingDirection = null;
        if (reason.Contains("Missing IN", StringComparison.OrdinalIgnoreCase))
        {
            missingDirection = "IN";
        }
        else if (reason.Contains("Missing OUT", StringComparison.OrdinalIgnoreCase))
        {
            missingDirection = "OUT";
        }

        if (missingDirection is null)
        {
            return null;
        }

        return new MissingTripNotificationSyncPayload
        {
            MissingDirection = missingDirection,
            NotificationOnly = true,
            SyntheticTripCreated = false,
            CountedAsTransaction = false,
            Message = $"Expected {missingDirection} trip was missing. " +
                      "Notification only; no separate missing trip was created, uploaded, or counted."
        };
    }

    private static ExceptionalApprovalSyncPayload? BuildExceptionalApprovalPayload(
        TripRecord trip)
    {
        if (trip.ExceptionalApprovedAt is { } approvedAt)
        {
            return CreateExceptionalApprovalPayload(
                ResolveApprovalMode(trip),
                trip.ExceptionalApprovalReason,
                trip.ExceptionalApproverName,
                trip.ExceptionalApproverRole,
                trip.ExceptionalApproverMobile,
                approvedAt);
        }

        return TryReadLegacyExceptionalApproval(trip.Notes, out var legacyApproval)
            ? CreateExceptionalApprovalPayload(
                legacyApproval.ApprovalMode,
                legacyApproval.Reason,
                legacyApproval.ApproverName,
                legacyApproval.ApproverRole,
                legacyApproval.ApproverMobile,
                legacyApproval.ApprovedAt)
            : null;
    }

    private static ExceptionalApprovalSyncPayload CreateExceptionalApprovalPayload(
        string approvalMode,
        string reason,
        string approverName,
        string approverRole,
        string approverMobile,
        DateTimeOffset approvedAt)
    {
        return new ExceptionalApprovalSyncPayload
        {
            ApprovalMode = NormalizeApprovalMode(approvalMode),
            Reason = reason,
            ApproverName = approverName,
            ApproverRole = approverRole,
            ApproverMobile = approverMobile,
            ApprovalDate = FormatDate(approvedAt),
            ApprovalTime = FormatTime(approvedAt),
            TimeZoneOffset = FormatOffset(approvedAt)
        };
    }

    private static string ResolveApprovalMode(TripRecord trip)
    {
        if (!string.IsNullOrWhiteSpace(trip.ExceptionalApprovalMode))
        {
            return NormalizeApprovalMode(trip.ExceptionalApprovalMode);
        }

        if (TryReadLegacyExceptionalApproval(trip.Notes, out var legacyApproval))
        {
            return legacyApproval.ApprovalMode;
        }

        return string.Equals(
            trip.ExceptionalApproverName,
            "SYSTEM",
            StringComparison.OrdinalIgnoreCase)
            ? "Auto"
            : "Manual";
    }

    private static string NormalizeApprovalMode(string? approvalMode) =>
        string.Equals(approvalMode, "Auto", StringComparison.OrdinalIgnoreCase)
            ? "Auto"
            : "Manual";

    private static bool TryReadLegacyExceptionalApproval(
        string notes,
        out LegacyExceptionalApproval approval)
    {
        approval = default;
        const string marker = "Exceptional approval:";

        var markerIndex = notes.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return false;
        }

        var metadata = notes[(markerIndex + marker.Length)..];
        var recordIndex = metadata.IndexOf("; Record=", StringComparison.OrdinalIgnoreCase);
        if (recordIndex >= 0)
        {
            metadata = metadata[..recordIndex];
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in metadata.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = item.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            values[item[..separatorIndex].Trim()] = item[(separatorIndex + 1)..].Trim();
        }

        if (!values.TryGetValue("ApprovedAt", out var approvedAtText) ||
            !DateTimeOffset.TryParse(
                approvedAtText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var approvedAt))
        {
            return false;
        }

        approval = new LegacyExceptionalApproval(
            NormalizeApprovalMode(GetValue(values, "Mode")),
            GetValue(values, "Reason"),
            GetValue(values, "ApproverName"),
            GetValue(values, "ApproverRole"),
            GetValue(values, "ApproverMobile"),
            approvedAt);
        return true;
    }

    private static string GetValue(
        IReadOnlyDictionary<string, string> values,
        string key) =>
        values.TryGetValue(key, out var value) ? value : string.Empty;

    private static string FormatDate(DateTimeOffset timestamp) =>
        timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatTime(DateTimeOffset timestamp) =>
        timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string FormatOffset(DateTimeOffset timestamp) =>
        timestamp.ToString("zzz", CultureInfo.InvariantCulture);

    private string ResolvePublishTopic()
    {
        var configuredTopic = options.Server.Mqtt.PublishTopic;
        var topic = string.IsNullOrWhiteSpace(configuredTopic)
            ? options.Server.Mqtt.BaseTopic
            : configuredTopic;

        return topic.Trim().Trim('/');
    }

    private async Task LogIdleStateOnceAsync(
        string message,
        CancellationToken cancellationToken)
    {
        if (string.Equals(_lastIdleMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastIdleMessage = message;
        await logger.ServerAsync(message, cancellationToken);
    }

    private Task DelayUntilNextCycleAsync(CancellationToken cancellationToken) =>
        Task.Delay(
            TimeSpan.FromSeconds(Math.Max(1, options.Server.SyncIntervalSeconds)),
            cancellationToken);

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        var initialSeconds = Math.Max(1, options.Server.InitialRetryDelaySeconds);
        var maximumSeconds = Math.Max(initialSeconds, options.Server.MaxRetryDelaySeconds);
        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var calculatedSeconds = initialSeconds * Math.Pow(2, exponent);
        return TimeSpan.FromSeconds(Math.Min(maximumSeconds, calculatedSeconds));
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture)
            : elapsed.ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);

    private static string FormatBytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var unitIndex = 0;
        while (bytes >= 1024 && unitIndex < units.Length - 1)
        {
            bytes /= 1024;
            unitIndex++;
        }

        return $"{bytes:0.##} {units[unitIndex]}";
    }

    private static string GetUsefulErrorMessage(Exception exception)
    {
        var current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    private sealed class TransactionSyncPayload
    {
        public required string SchemaVersion { get; init; }
        public required Guid TransactionId { get; init; }
        public required string SiteId { get; init; }
        public required string DeviceId { get; init; }
        public required string LaneId { get; init; }
        public required string DeviceName { get; init; }
        public required string Direction { get; init; }
        public required string RfidNumber { get; init; }
        public required string VehicleNumber { get; init; }
        public required string VehicleCategory { get; init; }
        public required string AccessType { get; init; }
        public string ContractorId { get; init; } = string.Empty;
        public string ContractorName { get; init; } = string.Empty;
        public string BalanceType { get; init; } = string.Empty;
        public string AuthorizationSource { get; init; } = string.Empty;
        public string AuthorizationRequestId { get; init; } = string.Empty;
        public string ServerDecision { get; init; } = string.Empty;
        public string ServerReason { get; init; } = string.Empty;
        public Guid? EntryTransactionId { get; init; }
        public decimal PreviousBalance { get; init; }
        public decimal DebitAmount { get; init; }
        public decimal NewBalance { get; init; }
        public required string ImageCaptureStatus { get; init; }
        public string? ImageFileName { get; init; }
        public string? ImageRemotePath { get; init; }
        public required string TripDate { get; init; }
        public required string TripTime { get; init; }
        public required string TimeZoneOffset { get; init; }
        public DateTimeOffset ProcessedAt { get; init; }
        public ExceptionalApprovalSyncPayload? ExceptionalApproval { get; init; }
        public MissingTripNotificationSyncPayload? MissingTripNotification { get; init; }
        public required string Notes { get; init; }
    }

    private sealed class MissingTripNotificationSyncPayload
    {
        public required string MissingDirection { get; init; }
        public bool NotificationOnly { get; init; }
        public bool SyntheticTripCreated { get; init; }
        public bool CountedAsTransaction { get; init; }
        public required string Message { get; init; }
    }

    private sealed class ExceptionalApprovalSyncPayload
    {
        public required string ApprovalMode { get; init; }
        public required string Reason { get; init; }
        public required string ApproverName { get; init; }
        public required string ApproverRole { get; init; }
        public required string ApproverMobile { get; init; }
        public required string ApprovalDate { get; init; }
        public required string ApprovalTime { get; init; }
        public required string TimeZoneOffset { get; init; }
    }

    private readonly record struct LegacyExceptionalApproval(
        string ApprovalMode,
        string Reason,
        string ApproverName,
        string ApproverRole,
        string ApproverMobile,
        DateTimeOffset ApprovedAt);
}
