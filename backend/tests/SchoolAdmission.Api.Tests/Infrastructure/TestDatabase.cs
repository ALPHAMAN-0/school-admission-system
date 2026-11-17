using Npgsql;

namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>
/// A throw-away PostgreSQL database on a real server. Tests run against real PostgreSQL (not an
/// in-memory fake) because the rules under test rely on row locks and partial unique indexes.
/// Server comes from TEST_DB_CONNECTION (see README); defaults to the local dev container.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private const string DefaultAdminConnection =
        "Host=localhost;Port=55432;Username=postgres;Password=postgres;Database=postgres";

    private readonly string _adminConnectionString;
    private readonly string _databaseName;

    private TestDatabase(string adminConnectionString, string databaseName, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var admin = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(admin))
        {
            admin = DefaultAdminConnection;
        }

        var databaseName = $"admission_test_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(admin)
        {
            Database = databaseName,
            MaxPoolSize = 25,
        };

        return new TestDatabase(admin, databaseName, builder.ConnectionString);
    }

    public async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
