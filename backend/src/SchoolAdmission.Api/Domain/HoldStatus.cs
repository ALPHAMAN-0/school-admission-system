namespace SchoolAdmission.Api.Domain;

/// <summary>Lifecycle of a seat hold (an admission application).</summary>
public enum HoldStatus
{
    Held,
    Confirmed,
    Expired,
    Cancelled,
}

public static class HoldStatusExtensions
{
    /// <summary>The wire/database representation used by the API contract.</summary>
    public static string ToApiValue(this HoldStatus status) => status switch
    {
        HoldStatus.Held => "HELD",
        HoldStatus.Confirmed => "CONFIRMED",
        HoldStatus.Expired => "EXPIRED",
        HoldStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown hold status."),
    };

    public static HoldStatus FromApiValue(string value) => value switch
    {
        "HELD" => HoldStatus.Held,
        "CONFIRMED" => HoldStatus.Confirmed,
        "EXPIRED" => HoldStatus.Expired,
        "CANCELLED" => HoldStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown hold status."),
    };
}
