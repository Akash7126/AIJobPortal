namespace JobPlatform.Reporting.Application;

/// <summary>Period keys for trends: day yyyy-MM-dd, week (Monday) yyyy-MM-dd, month yyyy-MM.</summary>
public static class Periods
{
    public static string Key(DateTime utc, string granularity) => granularity.ToLowerInvariant() switch
    {
        "month" => utc.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
        "week" => utc.Date.AddDays(-(((int)utc.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        _ => utc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
    };
}

/// <summary>Placeholder success type of the internal guard helper (keeps the handlers free of the Unit alias).</summary>
internal readonly record struct Unit2
{
    public static readonly Unit2 Value = default;
}
