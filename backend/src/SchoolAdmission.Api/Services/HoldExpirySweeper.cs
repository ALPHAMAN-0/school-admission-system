using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Services;

/// <summary>
/// Periodically rewrites HELD rows whose time is up to EXPIRED.
/// Correctness does not depend on this job: seat counts and statuses already treat such rows as
/// expired the moment <c>expires_at</c> passes. The sweeper keeps the stored data tidy.
/// <c>FOR UPDATE SKIP LOCKED</c> means it never waits for, or deadlocks with, a live request,
/// and several backend processes can run it at the same time.
/// </summary>
public sealed class HoldExpirySweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<AdmissionOptions> options,
    TimeProvider time,
    ILogger<HoldExpirySweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var expired = await SweepOnceAsync(stoppingToken);
                    if (expired > 0)
                    {
                        logger.LogInformation("Released {Count} expired seat hold(s).", expired);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Hold expiry sweep failed; it will run again on the next tick.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    public async Task<int> SweepOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        var now = time.GetUtcNow();

        return await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE holds SET status = 'EXPIRED'
            WHERE id IN (
                SELECT id FROM holds
                WHERE status = 'HELD' AND expires_at <= {now}
                FOR UPDATE SKIP LOCKED)
            """,
            cancellationToken);
    }
}
