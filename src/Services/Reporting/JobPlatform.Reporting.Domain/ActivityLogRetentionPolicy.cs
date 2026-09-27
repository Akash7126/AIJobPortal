using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>
/// Singleton (US-3.5.1-02): how long user-activity facts are kept live. Default 12 months (A-02-012), never below the legal minimum (INV-01, GAP-002, Q-02).
/// Administrator-only access (INV-02) is enforced by the request authorisation with the module's own error code.
/// </summary>
public sealed class ActivityLogRetentionPolicy : AggregateRoot<Guid>
{
    public const int DefaultMonths = 12;
    public const int MaxMonths = 120;

    /// <summary>The single row's key.</summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-0000000000a1");

    private ActivityLogRetentionPolicy()
    {
    }

    public int RetentionMonths { get; private set; }
    public int LegalMinimumMonths { get; private set; }
    public Guid? ChangedBy { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static ActivityLogRetentionPolicy CreateDefault(int legalMinimumMonths, DateTime nowUtc)
    {
        Guard.Ensure(legalMinimumMonths is >= 1 and <= MaxMonths, ReportingRuleCodes.RetentionBelowLegalMinimum, "The legal minimum retention is invalid.");
        return new ActivityLogRetentionPolicy
        {
            Id = SingletonId,
            RetentionMonths = Math.Max(DefaultMonths, legalMinimumMonths),
            LegalMinimumMonths = legalMinimumMonths,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>INV-01: months below the legal minimum are refused (E-UAM-INVALID-FIELD).</summary>
    public void Set(int months, Guid administratorId, DateTime nowUtc)
    {
        Guard.Ensure(months >= LegalMinimumMonths && months <= MaxMonths, ReportingRuleCodes.RetentionBelowLegalMinimum,
            $"The retention period must be between the legal minimum of {LegalMinimumMonths} months and {MaxMonths} months.",
            ReportingErrorCodes.ActivityInvalidField, BusinessRuleKind.InvalidInput);
        RetentionMonths = months;
        ChangedBy = administratorId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>Facts occurring before this instant are past retention.</summary>
    public DateTime CutoffUtc(DateTime nowUtc) => nowUtc.AddMonths(-RetentionMonths);

    /// <summary>The legal minimum is configuration (Q-02); a changed configuration is applied on the next start.</summary>
    public void UpdateLegalMinimum(int legalMinimumMonths)
    {
        Guard.Ensure(legalMinimumMonths is >= 1 and <= MaxMonths, ReportingRuleCodes.RetentionBelowLegalMinimum, "The legal minimum retention is invalid.");
        LegalMinimumMonths = legalMinimumMonths;
        if (RetentionMonths < legalMinimumMonths)
        {
            RetentionMonths = legalMinimumMonths;
        }
    }
}
