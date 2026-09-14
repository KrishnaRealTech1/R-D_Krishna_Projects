using System.Globalization;
using Microsoft.Data.Sqlite;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Data;

public sealed class ContractorRepository(SqliteConnectionFactory connectionFactory)
{
    public async Task<ContractorRecord?> GetByCodeAsync(string contractorCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contractorCode)) return null;
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT id, contractor_code, contractor_name, balance, is_active, updated_at, last_server_sync_at FROM contractors WHERE contractor_code=$code COLLATE NOCASE LIMIT 1;";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$code", contractorCode.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ContractorRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            ContractorCode = reader.GetString(1),
            ContractorName = reader.GetString(2),
            Balance = decimal.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
            IsActive = reader.GetInt32(4) != 0,
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture),
            LastServerSyncAt = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture)
        };
    }

    public async Task UpsertAsync(ContractorRecord contractor, CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO contractors(id, contractor_code, contractor_name, balance, is_active, updated_at, last_server_sync_at)
            VALUES($id,$code,$name,$balance,$active,$updated,$sync)
            ON CONFLICT(contractor_code) DO UPDATE SET
              contractor_name=excluded.contractor_name,
              balance=excluded.balance,
              is_active=excluded.is_active,
              updated_at=excluded.updated_at,
              last_server_sync_at=excluded.last_server_sync_at;
            """;
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", contractor.Id.ToString());
        command.Parameters.AddWithValue("$code", contractor.ContractorCode);
        command.Parameters.AddWithValue("$name", contractor.ContractorName);
        command.Parameters.AddWithValue("$balance", contractor.Balance.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$active", contractor.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$updated", contractor.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$sync", contractor.LastServerSyncAt?.ToString("O") ?? (object)DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertCategoryPriceAsync(string categoryName, decimal debitAmount, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return;
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = """
            INSERT INTO vehicle_category_prices(category_name,debit_amount,is_active,updated_at)
            VALUES($name,$amount,1,$updated)
            ON CONFLICT(category_name) DO UPDATE SET debit_amount=excluded.debit_amount,is_active=1,updated_at=excluded.updated_at;
            """;
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$name", categoryName.Trim());
        command.Parameters.AddWithValue("$amount", debitAmount.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updated", DateTimeOffset.Now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<decimal?> GetCategoryDebitAmountAsync(string categoryName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return null;
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT debit_amount FROM vehicle_category_prices WHERE category_name=$name COLLATE NOCASE AND is_active=1 LIMIT 1;";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$name", categoryName.Trim());
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? null : decimal.Parse(Convert.ToString(result, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
    }
}
