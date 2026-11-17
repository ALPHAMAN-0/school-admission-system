namespace SchoolAdmission.Api.Contracts;

// Request and response shapes of the public API. Field names follow the assignment contract
// exactly; a few extra fields are added where they help the UI (extra fields are allowed).

/// <summary>Body of <c>POST /classes</c>. Nullable so missing fields give a 400, not a default.</summary>
public sealed record CreateClassRequest(string? Name, int? TotalSeats);

/// <summary><c>POST /classes</c>, <c>GET /classes</c> and <c>GET /classes/{classId}</c>.</summary>
public sealed record ClassResponse(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);

/// <summary><c>POST /classes/{classId}/holds</c> (201).</summary>
public sealed record HoldCreatedResponse(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt,
    string ClassName,
    DateTimeOffset CreatedAt);

/// <summary><c>GET /holds/{holdId}</c>.</summary>
public sealed record HoldResponse(
    string HoldId,
    string ClassId,
    string Status,
    DateTimeOffset ExpiresAt,
    string ClassName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt);

/// <summary><c>POST /holds/{holdId}/confirm</c> (200).</summary>
public sealed record HoldConfirmedResponse(
    string HoldId,
    string Status,
    string ClassId,
    DateTimeOffset? ConfirmedAt);

/// <summary>One row of <c>GET /me/holds</c>.</summary>
public sealed record MyHoldResponse(
    string HoldId,
    string ClassId,
    string ClassName,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt);
