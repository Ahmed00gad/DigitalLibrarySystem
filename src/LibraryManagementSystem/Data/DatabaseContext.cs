using Microsoft.Data.Sqlite;

namespace LibraryManagementSystem.Data;

public class DatabaseContext
{
    private readonly string _connectionString;

    public DatabaseContext(string dbPath = "library.db")
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        System.Diagnostics.Debug.WriteLine(
    "DATABASE PATH: " + Path.GetFullPath(dbPath));
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        return connection;
    }

    public async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = CreateConnection();
        await connection.OpenAsync();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master " +
                "WHERE type='table' AND name='Members';";

            var count = Convert.ToInt32(
                await cmd.ExecuteScalarAsync());

            System.Diagnostics.Debug.WriteLine(
                "DATABASE FILE: " + connection.DataSource);

            System.Diagnostics.Debug.WriteLine(
                "MEMBERS TABLE EXISTS: " + (count == 1));
        }
        return connection;
    }
}
