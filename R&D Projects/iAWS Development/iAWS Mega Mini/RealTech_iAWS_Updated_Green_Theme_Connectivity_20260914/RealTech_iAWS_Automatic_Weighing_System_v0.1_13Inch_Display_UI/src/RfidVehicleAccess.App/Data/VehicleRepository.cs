using System.Globalization;
using Microsoft.Data.Sqlite;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Data;

public sealed class VehicleRepository(SqliteConnectionFactory connectionFactory)
{
    public async Task<VehicleRecord?> GetByRfidAsync(
        string rfidNumber,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT id, rfid_number, vehicle_number, source_site, vehicle_category,
                   access_type, balance, empty_weight, is_active, created_at,
                   updated_at, last_server_sync_at, contractor_code, contractor_name,
                   category_debit_amount
            FROM vehicles
            WHERE rfid_number = $rfid
            LIMIT 1;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$rfid", rfidNumber);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadVehicle(reader) : null;
    }

    public async Task<(bool Inserted, bool Updated, string? Error)> UpsertAsync(
        VehicleRecord vehicle,
        VehicleRecord? existing,
        bool updateExisting,
        CancellationToken cancellationToken = default)
    {
        if (existing is null)
        {
            await InsertAsync(vehicle, cancellationToken);
            return (true, false, null);
        }

        if (!string.Equals(existing.VehicleNumber, vehicle.VehicleNumber, StringComparison.OrdinalIgnoreCase))
        {
            return (false, false,
                $"RFID {vehicle.RfidNumber} is already assigned to vehicle {existing.VehicleNumber}.");
        }

        if (!updateExisting || HasSameMasterData(existing, vehicle))
        {
            return (false, false, null);
        }

        await UpdateMasterDataAsync(existing.Id, vehicle, cancellationToken);
        return (false, true, null);
    }

    public async Task UpdateBalanceAsync(
        Guid vehicleId,
        decimal balance,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE vehicles
            SET balance = $balance,
                updated_at = $updatedAt
            WHERE id = $id;
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$balance", ToInvariant(balance));
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.Now.ToString("O"));
        command.Parameters.AddWithValue("$id", vehicleId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqliteCommand("SELECT COUNT(*) FROM vehicles;", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static bool HasSameMasterData(VehicleRecord existing, VehicleRecord incoming) =>
        string.Equals(
            existing.VehicleNumber,
            incoming.VehicleNumber,
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(existing.SourceSite, incoming.SourceSite, StringComparison.Ordinal) &&
        string.Equals(
            existing.VehicleCategory,
            incoming.VehicleCategory,
            StringComparison.Ordinal) &&
        existing.AccessType == incoming.AccessType &&
        existing.Balance == incoming.Balance &&
        existing.EmptyWeight == incoming.EmptyWeight &&
        existing.IsActive == incoming.IsActive &&
        string.Equals(existing.ContractorCode, incoming.ContractorCode, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(existing.ContractorName, incoming.ContractorName, StringComparison.Ordinal) &&
        existing.CategoryDebitAmount == incoming.CategoryDebitAmount;

    private async Task InsertAsync(VehicleRecord vehicle, CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO vehicles (
                id, rfid_number, vehicle_number, source_site, vehicle_category,
                access_type, balance, empty_weight, is_active, created_at,
                updated_at, last_server_sync_at, contractor_code, contractor_name, category_debit_amount)
            VALUES (
                $id, $rfid, $vehicle, $sourceSite, $vehicleCategory,
                $accessType, $balance, $emptyWeight, $isActive, $createdAt,
                $updatedAt, $lastServerSyncAt, $contractorCode, $contractorName, $categoryDebitAmount);
            """;

        await using var command = new SqliteCommand(sql, connection);
        AddVehicleParameters(command, vehicle);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task UpdateMasterDataAsync(
        Guid existingId,
        VehicleRecord vehicle,
        CancellationToken cancellationToken)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            UPDATE vehicles
            SET vehicle_number = $vehicle,
                source_site = $sourceSite,
                vehicle_category = $vehicleCategory,
                access_type = $accessType,
                balance = $balance,
                empty_weight = $emptyWeight,
                is_active = $isActive,
                contractor_code = $contractorCode,
                contractor_name = $contractorName,
                category_debit_amount = $categoryDebitAmount,
                updated_at = $updatedAt
            WHERE id = $id;
            """;

        await using var command = new SqliteCommand(sql, connection);
        vehicle.UpdatedAt = DateTimeOffset.Now;
        command.Parameters.AddWithValue("$id", existingId.ToString());
        command.Parameters.AddWithValue("$vehicle", vehicle.VehicleNumber);
        command.Parameters.AddWithValue("$sourceSite", vehicle.SourceSite);
        command.Parameters.AddWithValue("$vehicleCategory", vehicle.VehicleCategory);
        command.Parameters.AddWithValue("$accessType", (int)vehicle.AccessType);
        command.Parameters.AddWithValue("$balance", ToInvariant(vehicle.Balance));
        command.Parameters.AddWithValue("$emptyWeight",
            vehicle.EmptyWeight is null ? DBNull.Value : ToInvariant(vehicle.EmptyWeight.Value));
        command.Parameters.AddWithValue("$isActive", vehicle.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$contractorCode", vehicle.ContractorCode);
        command.Parameters.AddWithValue("$contractorName", vehicle.ContractorName);
        command.Parameters.AddWithValue("$categoryDebitAmount", vehicle.CategoryDebitAmount is null ? DBNull.Value : ToInvariant(vehicle.CategoryDebitAmount.Value));
        command.Parameters.AddWithValue("$updatedAt", vehicle.UpdatedAt.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddVehicleParameters(SqliteCommand command, VehicleRecord vehicle)
    {
        command.Parameters.AddWithValue("$id", vehicle.Id.ToString());
        command.Parameters.AddWithValue("$rfid", vehicle.RfidNumber);
        command.Parameters.AddWithValue("$vehicle", vehicle.VehicleNumber);
        command.Parameters.AddWithValue("$sourceSite", vehicle.SourceSite);
        command.Parameters.AddWithValue("$vehicleCategory", vehicle.VehicleCategory);
        command.Parameters.AddWithValue("$accessType", (int)vehicle.AccessType);
        command.Parameters.AddWithValue("$balance", ToInvariant(vehicle.Balance));
        command.Parameters.AddWithValue("$emptyWeight",
            vehicle.EmptyWeight is null ? DBNull.Value : ToInvariant(vehicle.EmptyWeight.Value));
        command.Parameters.AddWithValue("$isActive", vehicle.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", vehicle.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", vehicle.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$lastServerSyncAt",
            vehicle.LastServerSyncAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$contractorCode", vehicle.ContractorCode);
        command.Parameters.AddWithValue("$contractorName", vehicle.ContractorName);
        command.Parameters.AddWithValue("$categoryDebitAmount", vehicle.CategoryDebitAmount is null ? DBNull.Value : ToInvariant(vehicle.CategoryDebitAmount.Value));
    }

    private static VehicleRecord ReadVehicle(SqliteDataReader reader)
    {
        var emptyWeightText = reader.IsDBNull(7) ? null : reader.GetString(7);
        var lastSyncText = reader.IsDBNull(11) ? null : reader.GetString(11);

        return new VehicleRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            RfidNumber = reader.GetString(1),
            VehicleNumber = reader.GetString(2),
            SourceSite = reader.GetString(3),
            VehicleCategory = reader.GetString(4),
            AccessType = (RfidAccessType)reader.GetInt32(5),
            Balance = ParseDecimal(reader.GetString(6)),
            EmptyWeight = string.IsNullOrWhiteSpace(emptyWeightText)
                ? null
                : ParseDecimal(emptyWeightText),
            IsActive = reader.GetInt32(8) == 1,
            CreatedAt = DateTimeOffset.Parse(reader.GetString(9), CultureInfo.InvariantCulture),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture),
            LastServerSyncAt = string.IsNullOrWhiteSpace(lastSyncText)
                ? null
                : DateTimeOffset.Parse(lastSyncText, CultureInfo.InvariantCulture),
            ContractorCode = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
            ContractorName = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
            CategoryDebitAmount = reader.IsDBNull(14) ? null : ParseDecimal(reader.GetString(14))
        };
    }

    private static string ToInvariant(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
