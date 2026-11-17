namespace SchoolAdmission.Api.Tests.Infrastructure;

/// <summary>A clock the tests can move forward, so expiry is tested without sleeping.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private long _utcTicks = start.UtcTicks;

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _utcTicks), TimeSpan.Zero);

    public void Advance(TimeSpan by) => Interlocked.Add(ref _utcTicks, by.Ticks);
}
