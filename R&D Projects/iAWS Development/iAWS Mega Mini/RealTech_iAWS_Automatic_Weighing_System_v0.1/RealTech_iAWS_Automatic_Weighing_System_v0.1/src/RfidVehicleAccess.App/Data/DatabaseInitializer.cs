using Microsoft.Data.Sqlite;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Data;

public sealed class DatabaseInitializer(SqliteConnectionFactory connectionFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS vehicles (
                id TEXT NOT NULL PRIMARY KEY,
                rfid_number TEXT NOT NULL COLLATE NOCASE UNIQUE,
                vehicle_number TEXT NOT NULL,
                source_site TEXT NOT NULL DEFAULT '',
                vehicle_category TEXT NOT NULL DEFAULT '',
                access_type INTEGER NOT NULL,
                balance TEXT NOT NULL DEFAULT '0',
                empty_weight TEXT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                last_server_sync_at TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_vehicles_vehicle_number
                ON vehicles(vehicle_number COLLATE NOCASE);

            CREATE TABLE IF NOT EXISTS contractors (
                id TEXT NOT NULL PRIMARY KEY,
                contractor_code TEXT NOT NULL COLLATE NOCASE UNIQUE,
                contractor_name TEXT NOT NULL,
                balance TEXT NOT NULL DEFAULT '0',
                is_active INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT NOT NULL,
                last_server_sync_at TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS vehicle_category_prices (
                category_name TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
                debit_amount TEXT NOT NULL DEFAULT '0',
                is_active INTEGER NOT NULL DEFAULT 1,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS trips (
                id TEXT NOT NULL PRIMARY KEY,
                site_id TEXT NOT NULL,
                device_id TEXT NOT NULL,
                direction INTEGER NOT NULL,
                rfid_number TEXT NOT NULL COLLATE NOCASE,
                vehicle_number TEXT NOT NULL,
                vehicle_category TEXT NOT NULL DEFAULT '',
                access_type INTEGER NOT NULL,
                entry_trip_id TEXT NULL,
                previous_balance TEXT NOT NULL,
                debit_amount TEXT NOT NULL,
                new_balance TEXT NOT NULL,
                image_path TEXT NOT NULL DEFAULT '',
                image_capture_status TEXT NOT NULL DEFAULT 'Pending',
                remote_image_path TEXT NOT NULL DEFAULT '',
                image_uploaded_at TEXT NULL,
                processed_at TEXT NOT NULL,
                status INTEGER NOT NULL,
                sync_attempt_count INTEGER NOT NULL DEFAULT 0,
                last_sync_attempt_at TEXT NULL,
                next_sync_attempt_at TEXT NULL,
                last_sync_error TEXT NOT NULL DEFAULT '',
                notes TEXT NOT NULL DEFAULT '',
                exceptional_approval_mode TEXT NOT NULL DEFAULT '',
                exceptional_approval_reason TEXT NOT NULL DEFAULT '',
                exceptional_approver_name TEXT NOT NULL DEFAULT '',
                exceptional_approver_role TEXT NOT NULL DEFAULT '',
                exceptional_approver_mobile TEXT NOT NULL DEFAULT '',
                exceptional_approved_at TEXT NULL,
                exceptionally_closed_at TEXT NULL,
                exceptionally_closed_by_trip_id TEXT NULL,
                synced_at TEXT NULL,
                iaws_data_json TEXT NOT NULL DEFAULT '',
                FOREIGN KEY(entry_trip_id) REFERENCES trips(id)
            );

            CREATE INDEX IF NOT EXISTS ix_trips_rfid_direction_status
                ON trips(rfid_number, direction, status);

            CREATE INDEX IF NOT EXISTS ix_trips_processed_at
                ON trips(processed_at DESC);

            CREATE INDEX IF NOT EXISTS ix_trips_status_synced_at
                ON trips(status, synced_at);

            CREATE TABLE IF NOT EXISTS rfid_recharges (
                recharge_id TEXT NOT NULL PRIMARY KEY,
                schema_version TEXT NOT NULL,
                rfid_number TEXT NOT NULL COLLATE NOCASE,
                vehicle_number TEXT NOT NULL DEFAULT '',
                recharge_amount TEXT NOT NULL,
                server_balance_before TEXT NULL,
                server_balance_after TEXT NULL,
                local_balance_before TEXT NULL,
                local_balance_after TEXT NULL,
                status TEXT NOT NULL,
                message TEXT NOT NULL DEFAULT '',
                site_id TEXT NOT NULL DEFAULT '',
                device_id TEXT NOT NULL DEFAULT '',
                recharged_at TEXT NULL,
                payment_reference TEXT NOT NULL DEFAULT '',
                operator_id TEXT NOT NULL DEFAULT '',
                operator_name TEXT NOT NULL DEFAULT '',
                remarks TEXT NOT NULL DEFAULT '',
                received_at TEXT NOT NULL,
                processed_at TEXT NOT NULL,
                raw_payload TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_rfid_recharges_rfid_processed_at
                ON rfid_recharges(rfid_number, processed_at DESC);

            CREATE TABLE IF NOT EXISTS import_audit (
                id TEXT NOT NULL PRIMARY KEY,
                file_name TEXT NOT NULL,
                imported_at TEXT NOT NULL,
                total_rows INTEGER NOT NULL,
                inserted INTEGER NOT NULL,
                updated INTEGER NOT NULL,
                skipped INTEGER NOT NULL,
                failed INTEGER NOT NULL,
                summary TEXT NOT NULL
            );
            """;

        await using (var command = new SqliteCommand(sql, connection))
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await EnsureVehicleColumnsAsync(connection, cancellationToken);
        await EnsureTripColumnsAsync(connection, cancellationToken);
        await ArchiveLegacySyntheticReconciliationTripsAsync(connection, cancellationToken);
        await CreatePostMigrationIndexesAsync(connection, cancellationToken);
    }


    private static async Task EnsureVehicleColumnsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using (var command = new SqliteCommand("PRAGMA table_info(vehicles);", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var migrations = new (string ColumnName, string Sql)[]
        {
            ("vehicle_category", "ALTER TABLE vehicles ADD COLUMN vehicle_category TEXT NOT NULL DEFAULT '';"),
            ("contractor_code", "ALTER TABLE vehicles ADD COLUMN contractor_code TEXT NOT NULL DEFAULT '';"),
            ("contractor_name", "ALTER TABLE vehicles ADD COLUMN contractor_name TEXT NOT NULL DEFAULT '';"),
            ("category_debit_amount", "ALTER TABLE vehicles ADD COLUMN category_debit_amount TEXT NULL;")
        };

        foreach (var migration in migrations)
        {
            if (existingColumns.Contains(migration.ColumnName)) continue;
            await using var command = new SqliteCommand(migration.Sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsureTripColumnsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using (var command = new SqliteCommand("PRAGMA table_info(trips);", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var migrations = new (string ColumnName, string Sql)[]
        {
            (
                "vehicle_category",
                "ALTER TABLE trips ADD COLUMN vehicle_category TEXT NOT NULL DEFAULT '';"),
            (
                "remote_image_path",
                "ALTER TABLE trips ADD COLUMN remote_image_path TEXT NOT NULL DEFAULT '';"),
            (
                "image_uploaded_at",
                "ALTER TABLE trips ADD COLUMN image_uploaded_at TEXT NULL;"),
            (
                "sync_attempt_count",
                "ALTER TABLE trips ADD COLUMN sync_attempt_count INTEGER NOT NULL DEFAULT 0;"),
            (
                "last_sync_attempt_at",
                "ALTER TABLE trips ADD COLUMN last_sync_attempt_at TEXT NULL;"),
            (
                "next_sync_attempt_at",
                "ALTER TABLE trips ADD COLUMN next_sync_attempt_at TEXT NULL;"),
            (
                "last_sync_error",
                "ALTER TABLE trips ADD COLUMN last_sync_error TEXT NOT NULL DEFAULT '';"),
            (
                "exceptional_approval_mode",
                "ALTER TABLE trips ADD COLUMN exceptional_approval_mode TEXT NOT NULL DEFAULT '';"),
            (
                "exceptional_approval_reason",
                "ALTER TABLE trips ADD COLUMN exceptional_approval_reason TEXT NOT NULL DEFAULT '';"),
            (
                "exceptional_approver_name",
                "ALTER TABLE trips ADD COLUMN exceptional_approver_name TEXT NOT NULL DEFAULT '';"),
            (
                "exceptional_approver_role",
                "ALTER TABLE trips ADD COLUMN exceptional_approver_role TEXT NOT NULL DEFAULT '';"),
            (
                "exceptional_approver_mobile",
                "ALTER TABLE trips ADD COLUMN exceptional_approver_mobile TEXT NOT NULL DEFAULT '';"),
            (
                "exceptional_approved_at",
                "ALTER TABLE trips ADD COLUMN exceptional_approved_at TEXT NULL;"),
            (
                "exceptionally_closed_at",
                "ALTER TABLE trips ADD COLUMN exceptionally_closed_at TEXT NULL;"),
            (
                "exceptionally_closed_by_trip_id",
                "ALTER TABLE trips ADD COLUMN exceptionally_closed_by_trip_id TEXT NULL;"),
            ("contractor_code", "ALTER TABLE trips ADD COLUMN contractor_code TEXT NOT NULL DEFAULT '';"),
            ("contractor_name", "ALTER TABLE trips ADD COLUMN contractor_name TEXT NOT NULL DEFAULT '';"),
            ("balance_type", "ALTER TABLE trips ADD COLUMN balance_type TEXT NOT NULL DEFAULT 'RFID';"),
            ("authorization_source", "ALTER TABLE trips ADD COLUMN authorization_source TEXT NOT NULL DEFAULT 'OFFLINE_LOCAL';"),
            ("authorization_request_id", "ALTER TABLE trips ADD COLUMN authorization_request_id TEXT NOT NULL DEFAULT '';"),
            ("server_decision", "ALTER TABLE trips ADD COLUMN server_decision TEXT NOT NULL DEFAULT '';"),
            ("server_reason", "ALTER TABLE trips ADD COLUMN server_reason TEXT NOT NULL DEFAULT '';"),
            ("iaws_data_json", "ALTER TABLE trips ADD COLUMN iaws_data_json TEXT NOT NULL DEFAULT '';")
        };

        foreach (var migration in migrations)
        {
            if (existingColumns.Contains(migration.ColumnName))
            {
                continue;
            }

            await using var command = new SqliteCommand(migration.Sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ArchiveLegacySyntheticReconciliationTripsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE trips
            SET status = $reconciliationRequired,
                next_sync_attempt_at = NULL,
                last_sync_error = '',
                notes = CASE
                    WHEN instr(notes, 'v0.1.43 notification-only migration') > 0 THEN notes
                    ELSE notes || ' v0.1.43 notification-only migration: synthetic missing trip archived locally; it is not uploaded or counted.'
                END
            WHERE image_capture_status = $syntheticCaptureStatus
              AND status <> $cancelled;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue(
            "$reconciliationRequired",
            (int)TripStatus.ReconciliationRequired);
        command.Parameters.AddWithValue(
            "$syntheticCaptureStatus",
            "NotCaptured-ExceptionalReconciliation");
        command.Parameters.AddWithValue("$cancelled", (int)TripStatus.Cancelled);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CreatePostMigrationIndexesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            CREATE INDEX IF NOT EXISTS ix_trips_pending_retry
                ON trips(status, next_sync_attempt_at, processed_at);

            CREATE INDEX IF NOT EXISTS ix_trips_exceptional_closure
                ON trips(exceptionally_closed_at, rfid_number, direction);
            """;

        await using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
