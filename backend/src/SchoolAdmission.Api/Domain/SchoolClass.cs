namespace SchoolAdmission.Api.Domain;

/// <summary>
/// One admission "class". Every shift is its own class with its own seat count,
/// e.g. "Class 6 - Morning Shift" and "Class 6 - Day Shift".
/// </summary>
public sealed class SchoolClass
{
    public const int MaxNameLength = 100;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required int TotalSeats { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public List<AdmissionHold> Holds { get; } = [];

    /// <summary>Trims and collapses inner whitespace: "  Class  6 " becomes "Class 6".</summary>
    public static string CleanName(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// False for text PostgreSQL cannot store or that makes no sense in a name: control characters
    /// (including NUL) and broken UTF-16 (unpaired surrogates).
    /// </summary>
    public static bool IsStorableText(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++; // a valid surrogate pair (e.g. an emoji)
                continue;
            }

            if (char.IsSurrogate(c) || char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }
}
