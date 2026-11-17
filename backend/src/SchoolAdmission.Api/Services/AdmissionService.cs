using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SchoolAdmission.Api.Contracts;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;
using SchoolAdmission.Api.Infrastructure;

namespace SchoolAdmission.Api.Services;

/// <summary>
/// All admission business rules. Seat accounting is protected by a row lock on the class
/// (<c>SELECT ... FOR UPDATE</c>) held inside a transaction, so concurrent applications for the
/// same class are serialised by PostgreSQL itself, across threads and across backend processes.
/// </summary>
public sealed class AdmissionService(AdmissionDbContext db, TimeProvider time, IOptions<AdmissionOptions> options)
{
    private const int MaxHoldsPerStudentListing = 500;

    public async Task<Result<ClassResponse>> CreateClassAsync(CreateClassRequest? request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request?.Name is null ? string.Empty : SchoolClass.CleanName(request.Name);
        if (name.Length == 0)
        {
            errors["name"] = ["Class name is required."];
        }
        else if (name.Length > SchoolClass.MaxNameLength)
        {
            errors["name"] = [$"Class name must be at most {SchoolClass.MaxNameLength} characters."];
        }

        if (request?.TotalSeats is not { } totalSeats)
        {
            errors["totalSeats"] = ["totalSeats is required."];
            totalSeats = 0;
        }
        else if (totalSeats is < 1 or > SchoolClass.MaxTotalSeats)
        {
            errors["totalSeats"] = [$"totalSeats must be a whole number between 1 and {SchoolClass.MaxTotalSeats}."];
        }

        if (errors.Count > 0)
        {
            return Errors.Validation(errors);
        }

        var normalizedName = SchoolClass.NormalizeName(name);
        if (await db.Classes.AnyAsync(c => c.NormalizedName == normalizedName, cancellationToken))
        {
            return Errors.DuplicateClassName(name);
        }

        var schoolClass = new SchoolClass
        {
            Id = IdGenerator.NewClassId(),
            Name = name,
            NormalizedName = normalizedName,
            TotalSeats = totalSeats,
            CreatedAt = time.GetUtcNow(),
        };
        db.Classes.Add(schoolClass);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two admins created the same name at the same moment.
            return Errors.DuplicateClassName(name);
        }

        return new ClassResponse(schoolClass.Id, schoolClass.Name, schoolClass.TotalSeats, 0, 0, schoolClass.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassResponse>> ListClassesAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        // Order before projecting: EF Core cannot translate ordering on a constructor projection.
        var orderedClasses = db.Classes.AsNoTracking().OrderBy(c => c.CreatedAt).ThenBy(c => c.Id);
        var rows = await ProjectSeatCounts(orderedClasses, now).ToListAsync(cancellationToken);

        return rows.Select(ToClassResponse).ToList();
    }

    public async Task<Result<ClassResponse>> GetClassAsync(string classId, CancellationToken cancellationToken)
    {
        if (!IdGenerator.IsPlausible(classId))
        {
            return Errors.ClassNotFound(classId);
        }

        var now = time.GetUtcNow();
        var row = await ProjectSeatCounts(db.Classes.AsNoTracking().Where(c => c.Id == classId), now)
            .SingleOrDefaultAsync(cancellationToken);

        return row is null ? Errors.ClassNotFound(classId) : ToClassResponse(row);
    }

    public async Task<Result<HoldCreatedResponse>> CreateHoldAsync(string classId, string studentId, CancellationToken cancellationToken)
    {
        if (!IdGenerator.IsPlausible(classId))
        {
            return Errors.ClassNotFound(classId);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // 1. Lock the class row. Every other application for this class, from this process or
        //    any other backend process, waits here until we commit or roll back.
        var lockedClass = await db.Database
            .SqlQuery<LockedClassRow>($"SELECT name AS \"Name\", total_seats AS \"TotalSeats\" FROM classes WHERE id = {classId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        if (lockedClass.Count == 0)
        {
            return Errors.ClassNotFound(classId);
        }

        var (className, totalSeats) = (lockedClass[0].Name, lockedClass[0].TotalSeats);
        var now = time.GetUtcNow();

        // 2. Holds whose time is up no longer occupy a seat. Mark them so the partial unique index
        //    (one active application per student per class) lets the student apply again.
        await db.Holds
            .Where(h => h.ClassId == classId && h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.Status, HoldStatus.Expired), cancellationToken);

        // 3. One active application or confirmed admission per student per class.
        var existing = await db.Holds
            .AsNoTracking()
            .Where(h => h.ClassId == classId
                && h.StudentId == studentId
                && (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed))
            .Select(h => new { h.Id, h.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return Errors.AlreadyApplied(existing.Id, existing.Status);
        }

        // 4. held + confirmed must stay <= totalSeats.
        var occupiedSeats = await db.Holds.CountAsync(
            h => h.ClassId == classId && (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed),
            cancellationToken);
        if (occupiedSeats >= totalSeats)
        {
            return Errors.NoSeatsAvailable();
        }

        var hold = new AdmissionHold
        {
            Id = IdGenerator.NewHoldId(),
            ClassId = classId,
            StudentId = studentId,
            CreatedAt = now,
            ExpiresAt = TruncateToSeconds(now) + options.Value.HoldDuration,
        };
        db.Holds.Add(hold);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new HoldCreatedResponse(
            hold.Id,
            hold.ClassId,
            hold.StudentId,
            HoldStatus.Held.ToApiValue(),
            hold.ExpiresAt,
            className,
            hold.CreatedAt);
    }

    public async Task<Result<HoldResponse>> GetHoldAsync(string holdId, CancellationToken cancellationToken)
    {
        if (!IdGenerator.IsPlausible(holdId))
        {
            return Errors.HoldNotFound(holdId);
        }

        var row = await db.Holds
            .AsNoTracking()
            .Where(h => h.Id == holdId)
            .Select(h => new { Hold = h, ClassName = h.Class!.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return Errors.HoldNotFound(holdId);
        }

        var now = time.GetUtcNow();
        var hold = row.Hold;
        return new HoldResponse(
            hold.Id,
            hold.ClassId,
            hold.EffectiveStatus(now).ToApiValue(),
            hold.ExpiresAt,
            row.ClassName,
            hold.CreatedAt,
            hold.ConfirmedAt);
    }

    public async Task<Result<HoldConfirmedResponse>> ConfirmHoldAsync(string holdId, string studentId, CancellationToken cancellationToken)
    {
        if (!IdGenerator.IsPlausible(holdId))
        {
            return Errors.HoldNotFound(holdId);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var hold = await LockHoldAsync(holdId, cancellationToken);
        if (hold is null)
        {
            return Errors.HoldNotFound(holdId);
        }

        if (hold.StudentId != studentId)
        {
            return Errors.NotHoldOwner();
        }

        var now = time.GetUtcNow();
        switch (hold.EffectiveStatus(now))
        {
            case HoldStatus.Confirmed:
                // Idempotent: a repeated confirm (double click, retry) changes nothing.
                return ToConfirmedResponse(hold);

            case HoldStatus.Cancelled:
                return Errors.HoldCancelled();

            case HoldStatus.Expired:
                hold.MarkExpired();
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Errors.HoldExpired(hold.ExpiresAt);
        }

        hold.Confirm(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToConfirmedResponse(hold);
    }

    public async Task<Result<string>> CancelHoldAsync(string holdId, string studentId, CancellationToken cancellationToken)
    {
        if (!IdGenerator.IsPlausible(holdId))
        {
            return Errors.HoldNotFound(holdId);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var hold = await LockHoldAsync(holdId, cancellationToken);
        if (hold is null)
        {
            return Errors.HoldNotFound(holdId);
        }

        if (hold.StudentId != studentId)
        {
            return Errors.NotHoldOwner();
        }

        var now = time.GetUtcNow();
        switch (hold.EffectiveStatus(now))
        {
            case HoldStatus.Cancelled:
                // Idempotent DELETE.
                return hold.Id;

            case HoldStatus.Confirmed:
                return Errors.HoldAlreadyConfirmed();

            case HoldStatus.Expired:
                hold.MarkExpired();
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Errors.HoldExpired(hold.ExpiresAt);
        }

        hold.Cancel(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return hold.Id;
    }

    public async Task<IReadOnlyList<MyHoldResponse>> ListStudentHoldsAsync(string studentId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var rows = await db.Holds
            .AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .ThenBy(h => h.Id)
            .Take(MaxHoldsPerStudentListing)
            .Select(h => new { Hold = h, ClassName = h.Class!.Name })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new MyHoldResponse(
                row.Hold.Id,
                row.Hold.ClassId,
                row.ClassName,
                row.Hold.EffectiveStatus(now).ToApiValue(),
                row.Hold.ExpiresAt,
                row.Hold.CreatedAt,
                row.Hold.ConfirmedAt))
            .ToList();
    }

    private async Task<AdmissionHold?> LockHoldAsync(string holdId, CancellationToken cancellationToken)
    {
        var rows = await db.Holds
            .FromSql($"SELECT * FROM holds WHERE id = {holdId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        return rows.SingleOrDefault();
    }

    private static IQueryable<SeatCountRow> ProjectSeatCounts(IQueryable<SchoolClass> classes, DateTimeOffset now) =>
        classes.Select(c => new SeatCountRow(
            c.Id,
            c.Name,
            c.TotalSeats,
            c.CreatedAt,
            c.Holds.Count(h => h.Status == HoldStatus.Held && h.ExpiresAt > now),
            c.Holds.Count(h => h.Status == HoldStatus.Confirmed)));

    private static ClassResponse ToClassResponse(SeatCountRow row) => new(
        row.Id,
        row.Name,
        row.TotalSeats,
        row.HeldSeats,
        row.ConfirmedSeats,
        Math.Max(0, row.TotalSeats - row.HeldSeats - row.ConfirmedSeats));

    private static HoldConfirmedResponse ToConfirmedResponse(AdmissionHold hold) =>
        new(hold.Id, HoldStatus.Confirmed.ToApiValue(), hold.ClassId, hold.ConfirmedAt);

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), value.Offset);

    private sealed record SeatCountRow(string Id, string Name, int TotalSeats, DateTimeOffset CreatedAt, int HeldSeats, int ConfirmedSeats);

    private sealed class LockedClassRow
    {
        public string Name { get; init; } = string.Empty;

        public int TotalSeats { get; init; }
    }
}
