using System.Diagnostics.CodeAnalysis;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// An expected, user-facing failure. Rendered as RFC 9457 problem details with a stable
/// machine-readable <see cref="Code"/> that the UI maps to a friendly message.
/// </summary>
public sealed record ApiError(int StatusCode, string Code, string Title, string Detail)
{
    public IReadOnlyDictionary<string, object?> Extensions { get; init; } = new Dictionary<string, object?>();

    public IResult ToResult()
    {
        var extensions = new Dictionary<string, object?>(Extensions) { ["code"] = Code };
        return TypedResults.Problem(detail: Detail, statusCode: StatusCode, title: Title, extensions: extensions);
    }
}

/// <summary>Success value or <see cref="ApiError"/>; used instead of exceptions for expected failures.</summary>
public readonly struct Result<T>
{
    private Result(T value)
    {
        Value = value;
        Error = null;
    }

    private Result(ApiError error)
    {
        Value = default;
        Error = error;
    }

    public T? Value { get; }

    public ApiError? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(ApiError error) => new(error);
}

/// <summary>Catalogue of every expected error the API can return.</summary>
public static class Errors
{
    public static ApiError MissingStudentId() => new(
        StatusCodes.Status400BadRequest,
        "MISSING_STUDENT_ID",
        "Student ID is required",
        $"Send the student's 17-digit birth registration number in the {StudentIdentity.HeaderName} header.");

    public static ApiError InvalidStudentId() => new(
        StatusCodes.Status400BadRequest,
        "INVALID_STUDENT_ID",
        "Invalid student ID",
        $"The {StudentIdentity.HeaderName} header must be exactly 17 digits (the birth registration number).");

    public static ApiError Validation(IDictionary<string, string[]> fieldErrors) => new(
        StatusCodes.Status400BadRequest,
        "VALIDATION_FAILED",
        "Validation failed",
        "One or more fields are invalid.")
    {
        Extensions = new Dictionary<string, object?> { ["errors"] = fieldErrors },
    };

    public static ApiError ClassNotFound(string classId) => new(
        StatusCodes.Status404NotFound,
        "CLASS_NOT_FOUND",
        "Class not found",
        $"No class exists with id '{Truncate(classId)}'.");

    public static ApiError HoldNotFound(string holdId) => new(
        StatusCodes.Status404NotFound,
        "HOLD_NOT_FOUND",
        "Application not found",
        $"No application exists with id '{Truncate(holdId)}'.");

    public static ApiError DuplicateClassName(string name) => new(
        StatusCodes.Status409Conflict,
        "CLASS_NAME_TAKEN",
        "Class already exists",
        $"A class named '{name}' already exists.");

    public static ApiError NoSeatsAvailable() => new(
        StatusCodes.Status409Conflict,
        "NO_SEATS_AVAILABLE",
        "No seats available",
        "All seats in this class are currently held or confirmed.");

    public static ApiError AlreadyApplied(string existingHoldId, HoldStatus status) => new(
        StatusCodes.Status409Conflict,
        "ALREADY_APPLIED",
        "Already applied",
        status == HoldStatus.Confirmed
            ? "This student is already admitted to this class."
            : "This student already has an active application for this class.")
    {
        Extensions = new Dictionary<string, object?>
        {
            // Not "status": that key is the HTTP status code in problem details.
            ["holdId"] = existingHoldId,
            ["holdStatus"] = status.ToApiValue(),
        },
    };

    public static ApiError NotHoldOwner() => new(
        StatusCodes.Status403Forbidden,
        "NOT_HOLD_OWNER",
        "Not your application",
        "This application belongs to a different student.");

    public static ApiError HoldExpired(DateTimeOffset expiresAt) => new(
        StatusCodes.Status409Conflict,
        "HOLD_EXPIRED",
        "Application expired",
        "The time to pay the admission fee has run out and the seat was released.")
    {
        Extensions = new Dictionary<string, object?> { ["expiresAt"] = expiresAt },
    };

    public static ApiError HoldCancelled() => new(
        StatusCodes.Status409Conflict,
        "HOLD_CANCELLED",
        "Application cancelled",
        "This application was cancelled.");

    public static ApiError HoldAlreadyConfirmed() => new(
        StatusCodes.Status409Conflict,
        "HOLD_ALREADY_CONFIRMED",
        "Admission already confirmed",
        "The admission fee is already paid, so this application can no longer be cancelled.");

    private static string Truncate(string value) => value.Length <= 40 ? value : value[..40] + "...";
}
