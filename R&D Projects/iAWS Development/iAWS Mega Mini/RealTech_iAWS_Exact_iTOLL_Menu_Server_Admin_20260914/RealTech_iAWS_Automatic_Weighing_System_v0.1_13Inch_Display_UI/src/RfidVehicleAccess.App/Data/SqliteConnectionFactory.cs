using Microsoft.Data.Sqlite;
using RfidVehicleAccess.Services;

namespace RfidVehicleAccess.Data;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(AppOptions options)
    {
        var databasePath = PathResolver.EnsureParentDirectory(options.Storage.DatabaseFile);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);
}
