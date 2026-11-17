using System.Globalization;

namespace SchoolAdmission.Api.Infrastructure;

public sealed class AdmissionOptions
{
    public const string HoldDurationKey = "HOLD_DURATION_SECONDS";
    public const string SweepIntervalKey = "HOLD_SWEEP_INTERVAL_SECONDS";
    public const int DefaultHoldDurationSeconds = 600;
    public const int DefaultSweepIntervalSeconds = 5;

    /// <summary>How long a seat stays held before the fee must be paid.</summary>
    public TimeSpan HoldDuration { get; set; } = TimeSpan.FromSeconds(DefaultHoldDurationSeconds);

    /// <summary>How often the background sweeper rewrites stale HELD rows to EXPIRED.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(DefaultSweepIntervalSeconds);

    /// <summary>Reads the options from environment variables / configuration. Fails fast on bad values.</summary>
    public static void Bind(AdmissionOptions options, IConfiguration configuration)
    {
        options.HoldDuration = TimeSpan.FromSeconds(ReadPositiveSeconds(configuration, HoldDurationKey, DefaultHoldDurationSeconds));
        options.SweepInterval = TimeSpan.FromSeconds(ReadPositiveSeconds(configuration, SweepIntervalKey, DefaultSweepIntervalSeconds));
    }

    private static int ReadPositiveSeconds(IConfiguration configuration, string key, int fallback)
    {
        var raw = configuration[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            return seconds;
        }

        throw new InvalidOperationException($"{key} must be a positive whole number of seconds, but was '{raw}'.");
    }
}
