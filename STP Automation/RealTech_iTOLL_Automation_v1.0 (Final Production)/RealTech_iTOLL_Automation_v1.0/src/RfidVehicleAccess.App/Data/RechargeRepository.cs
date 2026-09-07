using System.Globalization;
using Microsoft.Data.Sqlite;
using RfidVehicleAccess.Models;
using RfidVehicleAccess.Services;

namespace RfidVehicleAccess.Data;

public sealed class RechargeRepository(
    SqliteConnectionFactory connectionFactory,
    BalanceUpdateCoordinator balanceCoordinator)
{
    public async Task<RechargeApplyResult> ApplyAsync(
        RfidRechargeCommand command,
        string rawPayload,
        CancellationToken cancellationToken = default)
    {
        return await balanceCoordinator.RunAsync(async () =>
        {
            var receivedAt = DateTimeOffset.Now;
            await using var connection = connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();

            try
            {
                var duplicate = await ReadExistingAsync(
                    connection,
                    transaction,
                    command.RechargeId,
                    cancellationToken);
                if (duplicate is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return duplicate;
                }

                var vehicle = await ReadVehicleAsync(
                    connection,
                    transaction,
                    command.RfidNumber,
                    cancellationToken);

                RechargeApplyResult result;
                if (vehicle is null)
                {
                    result = CreateRejected(command, "RFID is not registered.", receivedAt);
                }
                else if (!vehicle.Value.IsActive)
                {
                    result = CreateRejected(
                        command,
                        "RFID is inactive.",
                        receivedAt,
                        vehicle.Value.VehicleNumber,
                        vehicle.Value.Balance);
                }
                else if (vehicle.Value.AccessType != RfidAccessType.Paid)
                {
                    result = CreateRejected(
                        command,
                        "Recharge is allowed only for Paid RFID cards.",
                        receivedAt,
                        vehicle.Value.VehicleNumber,
                        vehicle.Value.Balance);
                }
                else if (!string.IsNullOrWhiteSpace(command.VehicleNumber) &&
                         !string.Equals(
                             NormalizeVehicleNumber(command.VehicleNumber),
                             NormalizeVehicleNumber(vehicle.Value.VehicleNumber),
                             StringComparison.OrdinalIgnoreCase))
                {
                    result = CreateRejected(
                        command,
                        $"RFID belongs to vehicle {vehicle.Value.VehicleNumber}, " +
                        $"not {command.VehicleNumber}.",
                        receivedAt,
                        vehicle.Value.VehicleNumber,
                        vehicle.Value.Balance);
                }
                else
                {
                    var previousBalance = vehicle.Value.Balance;
                    var newBalance = previousBalance + command.RechargeAmount;

                    const string updateSql = """
                        UPDATE vehicles
                        SET balance = $balance,
                            updated_at = $updatedAt,
                            last_server_sync_at = $lastServerSyncAt
                        WHERE id = $id;
                        """;

                    await using var update =
                        new SqliteCommand(updateSql, connection, transaction);
                    update.Parameters.AddWithValue("$balance", ToInvariant(newBalance));
                    update.Parameters.AddWithValue("$updatedAt", receivedAt.ToString("O"));
                    update.Parameters.AddWithValue(
                        "$lastServerSyncAt",
                        receivedAt.ToString("O"));
                    update.Parameters.AddWithValue("$id", vehicle.Value.Id.ToString());
                    await update.ExecuteNonQueryAsync(cancellationToken);

                    result = new RechargeApplyResult
                    {
                        RechargeId = command.RechargeId,
                        RfidNumber = command.RfidNumber,
                        VehicleNumber = vehicle.Value.VehicleNumber,
                        Status = "Applied",
                        Success = true,
                        RechargeAmount = command.RechargeAmount,
                        PreviousBalance = previousBalance,
                        NewBalance = newBalance,
                        Message = "Recharge applied to the local RFID balance.",
                        ProcessedAt = receivedAt
                    };
                }

                await InsertAuditAsync(
                    connection,
                    transaction,
                    command,
                    result,
                    rawPayload,
                    receivedAt,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    private static async Task<RechargeApplyResult?> ReadExistingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string rechargeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT recharge_id, rfid_number, vehicle_number, status, recharge_amount,
                   local_balance_before, local_balance_after, message, processed_at
            FROM rfid_recharges
            WHERE recharge_id = $rechargeId
            LIMIT 1;
            """;

        await using var command = new SqliteCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("$rechargeId", rechargeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var originalStatus = reader.GetString(3);
        var originalMessage = reader.GetString(7);
        return new RechargeApplyResult
        {
            RechargeId = reader.GetString(0),
            RfidNumber = reader.GetString(1),
            VehicleNumber = reader.GetString(2),
            Status = "Duplicate",
            Success = string.Equals(originalStatus, "Applied", StringComparison.OrdinalIgnoreCase),
            IsDuplicate = true,
            RechargeAmount = ParseDecimal(reader.GetString(4)),
            PreviousBalance = reader.IsDBNull(5) ? null : ParseDecimal(reader.GetString(5)),
            NewBalance = reader.IsDBNull(6) ? null : ParseDecimal(reader.GetString(6)),
            Message = $"Recharge ID was already processed with status {originalStatus}: {originalMessage}",
            ProcessedAt = DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture)
        };
    }

    private static async Task<(Guid Id, string VehicleNumber, RfidAccessType AccessType, decimal Balance, bool IsActive)?>
        ReadVehicleAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string rfidNumber,
            CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, vehicle_number, access_type, balance, is_active
            FROM vehicles
            WHERE rfid_number = $rfid
            LIMIT 1;
            """;

        await using var command = new SqliteCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("$rfid", rfidNumber);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            (RfidAccessType)reader.GetInt32(2),
            ParseDecimal(reader.GetString(3)),
            reader.GetInt32(4) == 1);
    }

    private static RechargeApplyResult CreateRejected(
        RfidRechargeCommand command,
        string message,
        DateTimeOffset processedAt,
        string vehicleNumber = "",
        decimal? currentBalance = null) =>
        new()
        {
            RechargeId = command.RechargeId,
            RfidNumber = command.RfidNumber,
            VehicleNumber = vehicleNumber,
            Status = "Rejected",
            Success = false,
            RechargeAmount = command.RechargeAmount,
            PreviousBalance = currentBalance,
            NewBalance = currentBalance,
            Message = message,
            ProcessedAt = processedAt
        };

    private static async Task InsertAuditAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        RfidRechargeCommand source,
        RechargeApplyResult result,
        string rawPayload,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO rfid_recharges (
                recharge_id, schema_version, rfid_number, vehicle_number,
                recharge_amount, server_balance_before, server_balance_after,
                local_balance_before, local_balance_after, status, message,
                site_id, device_id, recharged_at, payment_reference,
                operator_id, operator_name, remarks, received_at, processed_at,
                raw_payload)
            VALUES (
                $rechargeId, $schemaVersion, $rfidNumber, $vehicleNumber,
                $rechargeAmount, $serverBalanceBefore, $serverBalanceAfter,
                $localBalanceBefore, $localBalanceAfter, $status, $message,
                $siteId, $deviceId, $rechargedAt, $paymentReference,
                $operatorId, $operatorName, $remarks, $receivedAt, $processedAt,
                $rawPayload);
            """;

        await using var command = new SqliteCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("$rechargeId", source.RechargeId);
        command.Parameters.AddWithValue("$schemaVersion", source.SchemaVersion);
        command.Parameters.AddWithValue("$rfidNumber", source.RfidNumber);
        command.Parameters.AddWithValue("$vehicleNumber", result.VehicleNumber);
        command.Parameters.AddWithValue("$rechargeAmount", ToInvariant(source.RechargeAmount));
        command.Parameters.AddWithValue("$serverBalanceBefore", ToDbDecimal(source.PreviousBalance));
        command.Parameters.AddWithValue("$serverBalanceAfter", ToDbDecimal(source.NewBalance));
        command.Parameters.AddWithValue("$localBalanceBefore", ToDbDecimal(result.PreviousBalance));
        command.Parameters.AddWithValue("$localBalanceAfter", ToDbDecimal(result.NewBalance));
        command.Parameters.AddWithValue("$status", result.Status);
        command.Parameters.AddWithValue("$message", result.Message);
        command.Parameters.AddWithValue("$siteId", source.SiteId);
        command.Parameters.AddWithValue("$deviceId", source.DeviceId);
        command.Parameters.AddWithValue("$rechargedAt", source.RechargedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$paymentReference", source.PaymentReference);
        command.Parameters.AddWithValue("$operatorId", source.OperatorId);
        command.Parameters.AddWithValue("$operatorName", source.OperatorName);
        command.Parameters.AddWithValue("$remarks", source.Remarks);
        command.Parameters.AddWithValue("$receivedAt", receivedAt.ToString("O"));
        command.Parameters.AddWithValue("$processedAt", result.ProcessedAt.ToString("O"));
        command.Parameters.AddWithValue("$rawPayload", rawPayload);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static object ToDbDecimal(decimal? value) =>
        value is null ? DBNull.Value : ToInvariant(value.Value);

    private static string ToInvariant(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string NormalizeVehicleNumber(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
}
