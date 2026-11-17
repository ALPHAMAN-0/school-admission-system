namespace SchoolAdmission.Api.Infrastructure;

/// <summary>
/// There is no login: the student is identified by the 17-digit birth registration number sent
/// in the <c>X-Student-Id</c> header.
/// </summary>
public static class StudentIdentity
{
    public const string HeaderName = "X-Student-Id";
    public const int Length = 17;

    public static Result<string> Parse(string? rawHeader)
    {
        if (string.IsNullOrWhiteSpace(rawHeader))
        {
            return Errors.MissingStudentId();
        }

        var value = rawHeader.Trim();
        if (value.Length != Length || !value.All(char.IsAsciiDigit))
        {
            return Errors.InvalidStudentId();
        }

        return value;
    }
}
