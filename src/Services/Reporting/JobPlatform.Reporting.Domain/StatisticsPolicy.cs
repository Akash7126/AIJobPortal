namespace JobPlatform.Reporting.Domain;

/// <summary>
/// Privacy and quality thresholds (handover 3.9, THR-046, Q-07). MinSampleSize decides when an aggregate is too thin to show
/// (RP.Stats.INSUFFICIENT_DATA); MinCell is the small-cell suppression threshold below which a breakdown cell is hidden.
/// </summary>
public sealed record StatisticsPolicy(int MinSampleSize = StatisticsPolicy.DefaultMinSampleSize, int MinCell = StatisticsPolicy.DefaultMinCell)
{
    public const int DefaultMinSampleSize = 10;
    public const int DefaultMinCell = 5;

    public bool IsInsufficient(long sampleSize) => sampleSize < MinSampleSize;

    /// <summary>A cell below MinCell is suppressed (never shown, never dropped silently: the caller flags it).</summary>
    public bool IsSuppressed(long cellCount) => cellCount < MinCell;
}

/// <summary>A figure that is either measured or explicitly flagged as insufficient (never a misleading number).</summary>
public readonly record struct Measured<T>(bool InsufficientData, T? Value, long SampleSize)
{
    public static Measured<T> Of(T value, long sampleSize) => new(false, value, sampleSize);

    public static Measured<T> Insufficient(long sampleSize) => new(true, default, sampleSize);
}

/// <summary>Pure calculators for the read models (handover section 10: "insufficient-data and small-cell rules (pure calculators)").</summary>
public static class StatisticsCalculator
{
    public const string Unspecified = "unspecified";

    public static Measured<T> Measure<T>(StatisticsPolicy policy, long sampleSize, Func<T> compute) =>
        policy.IsInsufficient(sampleSize) ? Measured<T>.Insufficient(sampleSize) : Measured<T>.Of(compute(), sampleSize);

    /// <summary>Missing or blank dimension values are counted under "unspecified" instead of being dropped (RP.Geo.UNSPECIFIED_BUCKET).</summary>
    public static string Bucket(string? value) => string.IsNullOrWhiteSpace(value) ? Unspecified : value.Trim();

    public static decimal? Ratio(long numerator, long denominator) => denominator <= 0 ? null : Math.Round((decimal)numerator / denominator, 4);

    /// <summary>Growth of the latest period over the previous one; null when there is no earlier period with data (insufficient history).</summary>
    public static decimal? Growth(long previous, long latest) => previous <= 0 ? null : Math.Round(((decimal)latest - previous) / previous, 4);

    public static decimal Midpoint(decimal? min, decimal? max) => (min, max) switch
    {
        (null, null) => 0m,
        (null, var hi) => hi!.Value,
        (var lo, null) => lo!.Value,
        var (lo, hi) => (lo!.Value + hi!.Value) / 2m
    };
}
