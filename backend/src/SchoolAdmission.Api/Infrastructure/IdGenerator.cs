using System.Security.Cryptography;

namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// Opaque, prefixed, random IDs (e.g. <c>c_9f2c4a1b7d3e8f60</c>). Hold IDs carry 96 random bits
/// because <c>GET /holds/{id}</c> needs no student header, so they must not be guessable.
/// </summary>
public static class IdGenerator
{
    public const int MaxLength = 40;

    public static string NewClassId() => "c_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));

    public static string NewHoldId() => "h_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12));

    /// <summary>Cheap pre-check so absurd route values return 404 without touching the database.</summary>
    public static bool IsPlausible(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= MaxLength;
}
