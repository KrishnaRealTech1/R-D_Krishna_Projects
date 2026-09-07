using Microsoft.Data.Sqlite;
using RfidVehicleAccess.Models;

namespace RfidVehicleAccess.Data;

public sealed class ImportAuditRepository(SqliteConnectionFactory connectionFactory)
{
    public async Task AddAsync(
        string fileName,
        ImportResult result,
        CancellationToken cancellationToken = default)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO import_audit (
                id, file_name, imported_at, total_rows,
                inserted, updated, skipped, failed, summary)
            VALUES (
                $id, $fileName, $importedAt, $totalRows,
                $inserted, $updated, $skipped, $failed, $summary);
            """;

        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$fileName", fileName);
        command.Parameters.AddWithValue("$importedAt", DateTimeOffset.Now.ToString("O"));
        command.Parameters.AddWithValue("$totalRows", result.TotalRows);
        command.Parameters.AddWithValue("$inserted", result.Inserted);
        command.Parameters.AddWithValue("$updated", result.Updated);
        command.Parameters.AddWithValue("$skipped", result.Skipped);
        command.Parameters.AddWithValue("$failed", result.Failed);
        command.Parameters.AddWithValue("$summary", result.ToSummary());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
