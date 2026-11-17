namespace SchoolAdmission.Api.Domain;

/// <summary>
/// A seat held for one student in one class. It becomes an admission when the fee is paid
/// (CONFIRMED) before <see cref="ExpiresAt"/>; otherwise the seat goes back automatically.
/// </summary>
public sealed class AdmissionHold
{
    public required string Id { get; init; }

    public required string ClassId { get; init; }

    public SchoolClass? Class { get; init; }

    /// <summary>17-digit birth registration number of the student.</summary>
    public required string StudentId { get; init; }

    public HoldStatus Status { get; private set; } = HoldStatus.Held;

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public DateTimeOffset? ConfirmedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// True when the hold is still marked HELD in storage but its time is up. Such a hold no
    /// longer occupies a seat, even before the background sweeper rewrites its status.
    /// </summary>
    public bool IsExpiredAt(DateTimeOffset now) => Status == HoldStatus.Held && ExpiresAt <= now;

    /// <summary>The status a client should see at <paramref name="now"/>.</summary>
    public HoldStatus EffectiveStatus(DateTimeOffset now) => IsExpiredAt(now) ? HoldStatus.Expired : Status;

    public void Confirm(DateTimeOffset now)
    {
        if (Status != HoldStatus.Held || IsExpiredAt(now))
        {
            throw new InvalidOperationException($"Only an active HELD hold can be confirmed (status: {EffectiveStatus(now).ToApiValue()}).");
        }

        Status = HoldStatus.Confirmed;
        ConfirmedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status != HoldStatus.Held || IsExpiredAt(now))
        {
            throw new InvalidOperationException($"Only an active HELD hold can be cancelled (status: {EffectiveStatus(now).ToApiValue()}).");
        }

        Status = HoldStatus.Cancelled;
        CancelledAt = now;
    }

    public void MarkExpired()
    {
        if (Status == HoldStatus.Held)
        {
            Status = HoldStatus.Expired;
        }
    }
}
