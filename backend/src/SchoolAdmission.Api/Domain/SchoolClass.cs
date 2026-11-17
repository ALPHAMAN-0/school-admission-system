namespace SchoolAdmission.Api.Domain;

/// <summary>
/// One admission "class". Every shift is its own class with its own seat count,
/// e.g. "Class 6 - Morning Shift" and "Class 6 - Day Shift".
/// </summary>
public sealed class SchoolClass
{
    public const int MaxNameLength = 100;
    public const int MaxTotalSeats = 10_000;

    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Upper-cased, whitespace-collapsed name used for the uniqueness check.</summary>
    public required string NormalizedName { get; init; }

    public required int TotalSeats { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public List<AdmissionHold> Holds { get; } = [];

    /// <summary>Trims and collapses inner whitespace so "Class  6 " and "Class 6" are the same name.</summary>
    public static string CleanName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string NormalizeName(string name) => CleanName(name).ToUpperInvariant();
}
