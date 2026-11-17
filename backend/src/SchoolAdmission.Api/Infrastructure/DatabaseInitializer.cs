using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Infrastructure;

public static class DatabaseInitializer
{
    /// <summary>Arbitrary application-wide key for the migration advisory lock.</summary>
    private const long MigrationLockKey = 7_301_542_118;

    /// <summary>
    /// Applies EF Core migrations before the app starts serving. Waits for PostgreSQL to accept
    /// connections, and takes a PostgreSQL advisory lock so several backend processes starting at
    /// the same time never migrate concurrently.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var maxAttempts = Math.Max(1, app.Configuration.GetValue("DB_STARTUP_MAX_ATTEMPTS", 30));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();

                await db.Database.OpenConnectionAsync(cancellationToken);
                try
                {
                    await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_lock({MigrationLockKey})", cancellationToken);
                    try
                    {
                        await db.Database.MigrateAsync(cancellationToken);
                    }
                    finally
                    {
                        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock({MigrationLockKey})", CancellationToken.None);
                    }
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }

                logger.LogInformation("Database is up to date.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsDatabaseNotReady(ex))
            {
                logger.LogWarning("Database is not reachable yet (attempt {Attempt}/{MaxAttempts}): {Reason}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private static bool IsDatabaseNotReady(Exception exception) => exception switch
    {
        // Server is up but still starting, or briefly out of connections.
        PostgresException postgres => postgres.SqlState is PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.TooManyConnections,
        NpgsqlException or SocketException or TimeoutException => true,
        _ => exception.InnerException is not null && IsDatabaseNotReady(exception.InnerException),
    };
}
