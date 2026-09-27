using System.Globalization;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>
/// Periodic (monthly by default, GAP-002) report for government stakeholders (US-3.5.2-08). INV: one per period - an existing report is returned, never duplicated
/// (unique index on Period as the final guard). Retained 12 months.
/// </summary>
public sealed class LaborMarketReport : AggregateRoot<Guid>
{
    public const int RetentionMonths = 12;
    public const string PeriodFormat = "yyyy-MM";

    private LaborMarketReport()
    {
    }

    /// <summary>Calendar month, yyyy-MM.</summary>
    public string Period { get; private set; } = string.Empty;
    public string ContentJson { get; private set; } = "{}";
    public Guid? GeneratedBy { get; private set; }
    public DateTime GeneratedAtUtc { get; private set; }
    public DateTime RetainUntilUtc { get; private set; }

    /// <summary>Parses "yyyy-MM" into the first and last instants of the month.</summary>
    public static bool TryParsePeriod(string? period, out DateTime startUtc, out DateTime endUtc)
    {
        startUtc = endUtc = default;
        if (!DateTime.TryParseExact(period, PeriodFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return false;
        }

        startUtc = new DateTime(parsed.Year, parsed.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        endUtc = startUtc.AddMonths(1);
        return true;
    }

    public static string PeriodOf(DateTime utc) => utc.ToString(PeriodFormat, CultureInfo.InvariantCulture);

    public static LaborMarketReport Generate(string period, string contentJson, Guid? generatedBy, DateTime nowUtc)
    {
        Guard.Ensure(TryParsePeriod(period, out var start, out _), ReportingRuleCodes.InvalidPeriod, "The period must be a calendar month formatted yyyy-MM.",
            ReportingErrorCodes.EmploymentInvalidField, BusinessRuleKind.InvalidInput);
        Guard.Ensure(start <= nowUtc, ReportingRuleCodes.InvalidPeriod, "A report cannot be generated for a future period.", ReportingErrorCodes.EmploymentInvalidField,
            BusinessRuleKind.InvalidInput);
        return new LaborMarketReport
        {
            Id = Guid.NewGuid(),
            Period = period,
            ContentJson = contentJson,
            GeneratedBy = generatedBy,
            GeneratedAtUtc = nowUtc,
            RetainUntilUtc = nowUtc.AddMonths(RetentionMonths)
        };
    }
}
