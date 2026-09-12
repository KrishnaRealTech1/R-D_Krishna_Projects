using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RfidVehicleAccess.Models;
using RfidVehicleAccess.Services;

namespace RfidVehicleAccess.Data;

public sealed class TripRepository(SqliteConnectionFactory connectionFactory)
{
    private const string SyntheticReconciliationCaptureStatus =
        "NotCaptured-ExceptionalReconciliation";

    private const string TripSelectColumns = """
        id, site_id, device_id, direction, rfid_number,
        vehicle_number, vehicle_category, access_type, entry_trip_id,
        previous_balance, debit_amount, new_balance,
        image_path, image_capture_status, remote_image_path, image_uploaded_at,
        processed_at, status, sync_attempt_count, last_sync_attempt_at,
        next_sync_attempt_at, last_sync_error, notes, synced_at,
        exceptional_approval_mode, exceptional_approval_reason, exceptional_approver_name,
        exceptional_approver_role, exceptional_approver_mobile,
        exceptional_approved_at, contractor_code, contractor_name, balance_type,
        authorization_source, authorization_request_id, server_decision, server_reason,
        iaws_data_json
        """;

    public async Task AddAsync(
        TripRecord trip,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await InsertTripAsync(connection, null, trip, cancellationToken);
    }

    public async Task<TripRecord?> GetActiveEntryAsync(
        string rfidNumber,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            SELECT {TripSelectColumns}
            FROM trips t
            WHERE t.rfid_number = $rfid
              AND t.direction = $inDirection
              AND t.status <> $cancelled
              AND t.exceptionally_closed_at IS NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM trips o
                  WHERE o.entry_trip_id = t.id
                    AND o.direction = $outDirection
                    AND o.status <> $cancelled)
            ORDER BY t.processed_at DESC
            LIMIT 1;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$rfid", rfidNumber);
        command.Parameters.AddWithValue("$inDirection", (int)LaneDirection.In);
        command.Parameters.AddWithValue("$outDirection", (int)LaneDirection.Out);
        command.Parameters.AddWithValue("$cancelled", (int)TripStatus.Cancelled);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTrip(reader) : null;
    }

    public async Task<(int ProcessedToday, int Pending)> GetDashboardCountsAsync(
        CancellationToken cancellationToken = default)
    {
        var localToday = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
        var localTomorrow = localToday.AddDays(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localToday, TimeZoneInfo.Local);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localTomorrow, TimeZoneInfo.Local);

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                (
                    SELECT COUNT(*)
                    FROM trips
                    WHERE status = $synced
                      AND image_capture_status <> $syntheticReconciliation
                      AND julianday(synced_at) >= julianday($startUtc)
                      AND julianday(synced_at) < julianday($endUtc)
                ) AS processed_today,
                (
                    SELECT COUNT(*)
                    FROM trips
                    WHERE status = $pending
                      AND image_capture_status <> $syntheticReconciliation
                ) AS pending;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$synced", (int)TripStatus.Synced);
        command.Parameters.AddWithValue("$pending", (int)TripStatus.PendingSync);
        command.Parameters.AddWithValue(
            "$syntheticReconciliation",
            SyntheticReconciliationCaptureStatus);
        command.Parameters.AddWithValue("$startUtc", startUtc.ToString("O"));
        command.Parameters.AddWithValue("$endUtc", endUtc.ToString("O"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0);
        }

        return (
            Convert.ToInt32(reader.GetInt64(0)),
            Convert.ToInt32(reader.GetInt64(1)));
    }

    public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqliteCommand(
            "SELECT COUNT(*) FROM trips " +
            "WHERE status = $status AND image_capture_status <> $syntheticReconciliation;",
            connection);
        command.Parameters.AddWithValue("$status", (int)TripStatus.PendingSync);
        command.Parameters.AddWithValue(
            "$syntheticReconciliation",
            SyntheticReconciliationCaptureStatus);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<(int DeletedCount, IReadOnlyList<string> ImagePaths)> DeletePendingAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        try
        {
            var imagePaths = new List<string>();
            await using (var imageCommand = new SqliteCommand(
                             "SELECT DISTINCT image_path FROM trips " +
                             "WHERE status = $pending " +
                             "AND image_capture_status <> $syntheticReconciliation " +
                             "AND image_path <> '';",
                             connection,
                             transaction))
            {
                imageCommand.Parameters.AddWithValue("$pending", (int)TripStatus.PendingSync);
                imageCommand.Parameters.AddWithValue(
                    "$syntheticReconciliation",
                    SyntheticReconciliationCaptureStatus);
                await using var reader = await imageCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (!reader.IsDBNull(0))
                    {
                        var imagePath = reader.GetString(0).Trim();
                        if (!string.IsNullOrWhiteSpace(imagePath))
                        {
                            imagePaths.Add(imagePath);
                        }
                    }
                }
            }

            // Remove links between pending records first so the foreign-key constraint
            // cannot block deletion of a pending IN/OUT pair.
            await using (var pendingUnlinkCommand = new SqliteCommand(
                             "UPDATE trips SET entry_trip_id = NULL " +
                             "WHERE status = $pending " +
                             "AND image_capture_status <> $syntheticReconciliation;",
                             connection,
                             transaction))
            {
                pendingUnlinkCommand.Parameters.AddWithValue("$pending", (int)TripStatus.PendingSync);
                pendingUnlinkCommand.Parameters.AddWithValue(
                    "$syntheticReconciliation",
                    SyntheticReconciliationCaptureStatus);
                await pendingUnlinkCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            // Keep already-synchronized history valid if it happened to point to a
            // pending entry that the operator is permanently clearing.
            await using (var unlinkCommand = new SqliteCommand(
                             "UPDATE trips SET entry_trip_id = NULL " +
                             "WHERE status <> $pending AND entry_trip_id IN " +
                             "(SELECT id FROM trips WHERE status = $pending " +
                             "AND image_capture_status <> $syntheticReconciliation);",
                             connection,
                             transaction))
            {
                unlinkCommand.Parameters.AddWithValue("$pending", (int)TripStatus.PendingSync);
                unlinkCommand.Parameters.AddWithValue(
                    "$syntheticReconciliation",
                    SyntheticReconciliationCaptureStatus);
                await unlinkCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            int deletedCount;
            await using (var deleteCommand = new SqliteCommand(
                             "DELETE FROM trips WHERE status = $pending " +
                             "AND image_capture_status <> $syntheticReconciliation;",
                             connection,
                             transaction))
            {
                deleteCommand.Parameters.AddWithValue("$pending", (int)TripStatus.PendingSync);
                deleteCommand.Parameters.AddWithValue(
                    "$syntheticReconciliation",
                    SyntheticReconciliationCaptureStatus);
                deletedCount = await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return (deletedCount, imagePaths);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<DateTimeOffset?> GetNextPendingRetryAtAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT next_sync_attempt_at
            FROM trips
            WHERE status = $status
              AND image_capture_status <> $syntheticReconciliation
              AND next_sync_attempt_at IS NOT NULL
            ORDER BY julianday(next_sync_attempt_at)
            LIMIT 1;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$status", (int)TripStatus.PendingSync);
        command.Parameters.AddWithValue(
            "$syntheticReconciliation",
            SyntheticReconciliationCaptureStatus);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        var text = value is null || value is DBNull
            ? null
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(text)
            ? null
            : DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<TripRecord>> GetPendingAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            SELECT {TripSelectColumns}
            FROM trips
            WHERE status = $status
              AND image_capture_status <> $syntheticReconciliation
              AND (
                  next_sync_attempt_at IS NULL OR
                  julianday(next_sync_attempt_at) <= julianday($now))
            ORDER BY julianday(COALESCE(next_sync_attempt_at, processed_at)), julianday(processed_at)
            LIMIT $limit;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$status", (int)TripStatus.PendingSync);
        command.Parameters.AddWithValue(
            "$syntheticReconciliation",
            SyntheticReconciliationCaptureStatus);
        command.Parameters.AddWithValue("$now", DateTimeOffset.Now.ToString("O"));
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<TripRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadTrip(reader));
        }

        return items;
    }

    public async Task MarkImageUploadedAsync(
        Guid tripId,
        string remoteImagePath,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE trips
            SET remote_image_path = $remoteImagePath,
                image_uploaded_at = $imageUploadedAt
            WHERE id = $id;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$remoteImagePath", remoteImagePath);
        command.Parameters.AddWithValue("$imageUploadedAt", uploadedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", tripId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkSyncFailureAsync(
        Guid tripId,
        int attemptCount,
        DateTimeOffset attemptedAt,
        DateTimeOffset nextAttemptAt,
        string error,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE trips
            SET sync_attempt_count = $attemptCount,
                last_sync_attempt_at = $attemptedAt,
                next_sync_attempt_at = $nextAttemptAt,
                last_sync_error = $error
            WHERE id = $id;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$attemptCount", attemptCount);
        command.Parameters.AddWithValue("$attemptedAt", attemptedAt.ToString("O"));
        command.Parameters.AddWithValue("$nextAttemptAt", nextAttemptAt.ToString("O"));
        command.Parameters.AddWithValue("$error", error);
        command.Parameters.AddWithValue("$id", tripId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkSyncedAsync(
        Guid tripId,
        int attemptCount,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var syncedAt = DateTimeOffset.Now;
        const string sql = """
            UPDATE trips
            SET status = $status,
                synced_at = $syncedAt,
                sync_attempt_count = $attemptCount,
                last_sync_attempt_at = $attemptedAt,
                next_sync_attempt_at = NULL,
                last_sync_error = ''
            WHERE id = $id;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$status", (int)TripStatus.Synced);
        command.Parameters.AddWithValue("$syncedAt", syncedAt.ToString("O"));
        command.Parameters.AddWithValue("$attemptCount", attemptCount);
        command.Parameters.AddWithValue("$attemptedAt", attemptedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", tripId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task MarkEntryExceptionallyClosedAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid activeEntryId,
        Guid closedByTripId,
        DateTimeOffset closedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE trips
            SET exceptionally_closed_at = $closedAt,
                exceptionally_closed_by_trip_id = $closedByTripId
            WHERE id = $activeEntryId
              AND direction = $inDirection
              AND exceptionally_closed_at IS NULL
              AND NOT EXISTS (
                  SELECT 1
                  FROM trips o
                  WHERE o.entry_trip_id = trips.id
                    AND o.direction = $outDirection
                    AND o.status <> $cancelled);
            """;

        await using var command = new SqliteCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("$closedAt", closedAt.ToString("O"));
        command.Parameters.AddWithValue("$closedByTripId", closedByTripId.ToString());
        command.Parameters.AddWithValue("$activeEntryId", activeEntryId.ToString());
        command.Parameters.AddWithValue("$inDirection", (int)LaneDirection.In);
        command.Parameters.AddWithValue("$outDirection", (int)LaneDirection.Out);
        command.Parameters.AddWithValue("$cancelled", (int)TripStatus.Cancelled);

        var updated = await command.ExecuteNonQueryAsync(cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException(
                $"Active IN trip {activeEntryId} could not be closed for missing OUT notification.");
        }
    }

    private static async Task InsertTripAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        TripRecord trip,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO trips (
                id, site_id, device_id, direction, rfid_number,
                vehicle_number, vehicle_category, access_type, entry_trip_id,
                previous_balance, debit_amount, new_balance,
                image_path, image_capture_status, remote_image_path, image_uploaded_at,
                processed_at, status, sync_attempt_count, last_sync_attempt_at,
                next_sync_attempt_at, last_sync_error, notes, synced_at,
                exceptional_approval_mode, exceptional_approval_reason, exceptional_approver_name,
                exceptional_approver_role, exceptional_approver_mobile,
                exceptional_approved_at, contractor_code, contractor_name, balance_type,
                authorization_source, authorization_request_id, server_decision, server_reason, iaws_data_json)
            VALUES (
                $id, $siteId, $deviceId, $direction, $rfid,
                $vehicle, $vehicleCategory, $accessType, $entryTripId,
                $previousBalance, $debitAmount, $newBalance,
                $imagePath, $imageCaptureStatus, $remoteImagePath, $imageUploadedAt,
                $processedAt, $status, $syncAttemptCount, $lastSyncAttemptAt,
                $nextSyncAttemptAt, $lastSyncError, $notes, $syncedAt,
                $exceptionalApprovalMode, $exceptionalApprovalReason, $exceptionalApproverName,
                $exceptionalApproverRole, $exceptionalApproverMobile,
                $exceptionalApprovedAt, $contractorCode, $contractorName, $balanceType,
                $authorizationSource, $authorizationRequestId, $serverDecision, $serverReason, $iawsDataJson);
            """;

        await using var command = new SqliteCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("$id", trip.Id.ToString());
        command.Parameters.AddWithValue("$siteId", trip.SiteId);
        command.Parameters.AddWithValue("$deviceId", trip.DeviceId);
        command.Parameters.AddWithValue("$direction", (int)trip.Direction);
        command.Parameters.AddWithValue("$rfid", trip.RfidNumber);
        command.Parameters.AddWithValue("$vehicle", trip.VehicleNumber);
        command.Parameters.AddWithValue("$vehicleCategory", trip.VehicleCategory);
        command.Parameters.AddWithValue("$accessType", (int)trip.AccessType);
        command.Parameters.AddWithValue("$entryTripId",
            trip.EntryTripId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$previousBalance", ToInvariant(trip.PreviousBalance));
        command.Parameters.AddWithValue("$debitAmount", ToInvariant(trip.DebitAmount));
        command.Parameters.AddWithValue("$newBalance", ToInvariant(trip.NewBalance));
        command.Parameters.AddWithValue("$imagePath", trip.ImagePath);
        command.Parameters.AddWithValue("$imageCaptureStatus", trip.ImageCaptureStatus);
        command.Parameters.AddWithValue("$remoteImagePath", trip.RemoteImagePath);
        command.Parameters.AddWithValue("$imageUploadedAt",
            trip.ImageUploadedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$processedAt", trip.ProcessedAt.ToString("O"));
        command.Parameters.AddWithValue("$status", (int)trip.Status);
        command.Parameters.AddWithValue("$syncAttemptCount", trip.SyncAttemptCount);
        command.Parameters.AddWithValue("$lastSyncAttemptAt",
            trip.LastSyncAttemptAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$nextSyncAttemptAt",
            trip.NextSyncAttemptAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$lastSyncError", trip.LastSyncError);
        command.Parameters.AddWithValue("$notes", trip.Notes);
        command.Parameters.AddWithValue("$syncedAt",
            trip.SyncedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue(
            "$exceptionalApprovalMode",
            trip.ExceptionalApprovalMode);
        command.Parameters.AddWithValue(
            "$exceptionalApprovalReason",
            trip.ExceptionalApprovalReason);
        command.Parameters.AddWithValue(
            "$exceptionalApproverName",
            trip.ExceptionalApproverName);
        command.Parameters.AddWithValue(
            "$exceptionalApproverRole",
            trip.ExceptionalApproverRole);
        command.Parameters.AddWithValue(
            "$exceptionalApproverMobile",
            trip.ExceptionalApproverMobile);
        command.Parameters.AddWithValue(
            "$exceptionalApprovedAt",
            trip.ExceptionalApprovedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$contractorCode", trip.ContractorCode);
        command.Parameters.AddWithValue("$contractorName", trip.ContractorName);
        command.Parameters.AddWithValue("$balanceType", trip.BalanceType);
        command.Parameters.AddWithValue("$authorizationSource", trip.AuthorizationSource);
        command.Parameters.AddWithValue("$authorizationRequestId", trip.AuthorizationRequestId);
        command.Parameters.AddWithValue("$serverDecision", trip.ServerDecision);
        command.Parameters.AddWithValue("$serverReason", trip.ServerReason);
        command.Parameters.AddWithValue("$iawsDataJson", SerializeIawsData(trip));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static TripRecord ReadTrip(SqliteDataReader reader)
    {
        var trip = new TripRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            SiteId = reader.GetString(1),
            DeviceId = reader.GetString(2),
            Direction = (LaneDirection)reader.GetInt32(3),
            RfidNumber = reader.GetString(4),
            VehicleNumber = reader.GetString(5),
            VehicleCategory = reader.GetString(6),
            AccessType = (RfidAccessType)reader.GetInt32(7),
            EntryTripId = reader.IsDBNull(8) ? null : Guid.Parse(reader.GetString(8)),
            PreviousBalance = ParseDecimal(reader.GetString(9)),
            DebitAmount = ParseDecimal(reader.GetString(10)),
            NewBalance = ParseDecimal(reader.GetString(11)),
            ImagePath = reader.GetString(12),
            ImageCaptureStatus = reader.GetString(13),
            RemoteImagePath = reader.GetString(14),
            ImageUploadedAt = ReadDateTimeOffset(reader, 15),
            ProcessedAt = DateTimeOffset.Parse(reader.GetString(16), CultureInfo.InvariantCulture),
            Status = (TripStatus)reader.GetInt32(17),
            SyncAttemptCount = reader.GetInt32(18),
            LastSyncAttemptAt = ReadDateTimeOffset(reader, 19),
            NextSyncAttemptAt = ReadDateTimeOffset(reader, 20),
            LastSyncError = reader.GetString(21),
            Notes = reader.GetString(22),
            SyncedAt = ReadDateTimeOffset(reader, 23),
            ExceptionalApprovalMode = reader.GetString(24),
            ExceptionalApprovalReason = reader.GetString(25),
            ExceptionalApproverName = reader.GetString(26),
            ExceptionalApproverRole = reader.GetString(27),
            ExceptionalApproverMobile = reader.GetString(28),
            ExceptionalApprovedAt = ReadDateTimeOffset(reader, 29),
            ContractorCode = reader.GetString(30),
            ContractorName = reader.GetString(31),
            BalanceType = reader.GetString(32),
            AuthorizationSource = reader.GetString(33),
            AuthorizationRequestId = reader.GetString(34),
            ServerDecision = reader.GetString(35),
            ServerReason = reader.GetString(36)
        };

        if (!reader.IsDBNull(37))
        {
            ApplyIawsData(reader.GetString(37), trip);
        }

        return trip;
    }

    public async Task UpdateIawsDataAsync(
        TripRecord trip,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE trips SET iaws_data_json = $data WHERE id = $id;";
        command.Parameters.AddWithValue("$data", SerializeIawsData(trip));
        command.Parameters.AddWithValue("$id", trip.Id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SerializeIawsData(TripRecord trip) =>
        JsonSerializer.Serialize(new IawsTripData
        {
            WeightKg = trip.WeightKg,
            TargetWeightKg = trip.TargetWeightKg,
            Camera1ImagePath = trip.Camera1ImagePath,
            Camera1ImageCaptureStatus = trip.Camera1ImageCaptureStatus,
            Camera1RemoteImagePath = trip.Camera1RemoteImagePath,
            Camera1ImageUploadedAt = trip.Camera1ImageUploadedAt,
            Camera2ImagePath = trip.Camera2ImagePath,
            Camera2ImageCaptureStatus = trip.Camera2ImageCaptureStatus,
            Camera2RemoteImagePath = trip.Camera2RemoteImagePath,
            Camera2ImageUploadedAt = trip.Camera2ImageUploadedAt,
            Camera3ImagePath = trip.Camera3ImagePath,
            Camera3ImageCaptureStatus = trip.Camera3ImageCaptureStatus,
            Camera3RemoteImagePath = trip.Camera3RemoteImagePath,
            Camera3ImageUploadedAt = trip.Camera3ImageUploadedAt,
            Camera4ImagePath = trip.Camera4ImagePath,
            Camera4ImageCaptureStatus = trip.Camera4ImageCaptureStatus,
            Camera4RemoteImagePath = trip.Camera4RemoteImagePath,
            Camera4ImageUploadedAt = trip.Camera4ImageUploadedAt
        });

    private static void ApplyIawsData(string json, TripRecord trip)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var data = JsonSerializer.Deserialize<IawsTripData>(json);
            if (data is null) return;
            trip.WeightKg = data.WeightKg;
            trip.TargetWeightKg = data.TargetWeightKg;
            trip.Camera1ImagePath = data.Camera1ImagePath ?? string.Empty;
            trip.Camera1ImageCaptureStatus = data.Camera1ImageCaptureStatus ?? "Pending";
            trip.Camera1RemoteImagePath = data.Camera1RemoteImagePath ?? string.Empty;
            trip.Camera1ImageUploadedAt = data.Camera1ImageUploadedAt;
            trip.Camera2ImagePath = data.Camera2ImagePath ?? string.Empty;
            trip.Camera2ImageCaptureStatus = data.Camera2ImageCaptureStatus ?? "Pending";
            trip.Camera2RemoteImagePath = data.Camera2RemoteImagePath ?? string.Empty;
            trip.Camera2ImageUploadedAt = data.Camera2ImageUploadedAt;
            trip.Camera3ImagePath = data.Camera3ImagePath ?? string.Empty;
            trip.Camera3ImageCaptureStatus = data.Camera3ImageCaptureStatus ?? "Pending";
            trip.Camera3RemoteImagePath = data.Camera3RemoteImagePath ?? string.Empty;
            trip.Camera3ImageUploadedAt = data.Camera3ImageUploadedAt;
            trip.Camera4ImagePath = data.Camera4ImagePath ?? string.Empty;
            trip.Camera4ImageCaptureStatus = data.Camera4ImageCaptureStatus ?? "Pending";
            trip.Camera4RemoteImagePath = data.Camera4RemoteImagePath ?? string.Empty;
            trip.Camera4ImageUploadedAt = data.Camera4ImageUploadedAt;

            if (string.IsNullOrWhiteSpace(trip.Camera1ImagePath))
            {
                trip.Camera1ImagePath = trip.ImagePath;
                trip.Camera1ImageCaptureStatus = trip.ImageCaptureStatus;
                trip.Camera1RemoteImagePath = trip.RemoteImagePath;
                trip.Camera1ImageUploadedAt = trip.ImageUploadedAt;
            }
        }
        catch (JsonException)
        {
            // Older transactions contain no iAWS JSON and remain readable.
        }
    }

    private sealed class IawsTripData
    {
        public decimal WeightKg { get; set; }
        public decimal TargetWeightKg { get; set; }
        public string? Camera1ImagePath { get; set; }
        public string? Camera1ImageCaptureStatus { get; set; }
        public string? Camera1RemoteImagePath { get; set; }
        public DateTimeOffset? Camera1ImageUploadedAt { get; set; }
        public string? Camera2ImagePath { get; set; }
        public string? Camera2ImageCaptureStatus { get; set; }
        public string? Camera2RemoteImagePath { get; set; }
        public DateTimeOffset? Camera2ImageUploadedAt { get; set; }
        public string? Camera3ImagePath { get; set; }
        public string? Camera3ImageCaptureStatus { get; set; }
        public string? Camera3RemoteImagePath { get; set; }
        public DateTimeOffset? Camera3ImageUploadedAt { get; set; }
        public string? Camera4ImagePath { get; set; }
        public string? Camera4ImageCaptureStatus { get; set; }
        public string? Camera4RemoteImagePath { get; set; }
        public DateTimeOffset? Camera4ImageUploadedAt { get; set; }
    }

    private static DateTimeOffset? ReadDateTimeOffset(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : DateTimeOffset.Parse(reader.GetString(ordinal), CultureInfo.InvariantCulture);

    private static string ToInvariant(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
