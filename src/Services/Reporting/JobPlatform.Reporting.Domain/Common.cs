using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain;

/// <summary>Report category a role may be granted (handover 3.6). Administration (access rules) is not a category: it is always administrator-only.</summary>
public enum ReportCategory
{
    ActivityLogs,
    EmploymentStatistics,
    SystemPerformance,
    Custom
}

public enum ReportFormat
{
    Pdf,
    Excel,
    Csv
}

public enum ReportDataSource
{
    Activity,
    Employment,
    Performance
}

public enum ParameterType
{
    Int,
    Decimal,
    Date,
    Enum,
    String
}

public enum ScheduleInterval
{
    Daily,
    Weekly,
    Monthly,
    Cron
}

public enum ExportStatus
{
    Queued,
    Generating,
    Ready,
    Failed
}

public enum AlertComparator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public enum ReportTarget
{
    Builtin,
    PowerBi
}

/// <summary>What a schedule or export points at.</summary>
public enum ReportRefKind
{
    Template,
    SavedReport,
    LaborMarket
}

/// <summary>Externally published error codes (handover sections 3, 4 and 6).</summary>
public static class ReportingErrorCodes
{
    public const string ActivityForbidden = "E-UAM-FORBIDDEN";
    public const string ActivityInvalidField = "E-UAM-INVALID-FIELD";
    public const string EmploymentForbidden = "E-EMPST-FORBIDDEN";
    public const string EmploymentInvalidField = "E-EMPST-INVALID-FIELD";
    public const string PerformanceForbidden = "E-SPM-FORBIDDEN";
    public const string PerformanceInvalidField = "E-SPM-INVALID-FIELD";
    public const string CustomForbidden = "E-CRG-FORBIDDEN";
    public const string CustomInvalidField = "E-CRG-INVALID-FIELD";
    public const string CustomUpstreamTimeout = "E-CRG-UPSTREAM-TIMEOUT";
    public const string NotFound = "E-CRG-NOT-FOUND";
    public const string ExportDuplicate = "E-CRG-EXPORT-DUPLICATE";
    public const string ExportNotReady = "E-CRG-EXPORT-NOT-READY";
    public const string LinkInvalid = "E-CRG-LINK-INVALID";
}

/// <summary>Internal business-rule codes, format RP.Aggregate.RULE (handover 4).</summary>
public static class ReportingRuleCodes
{
    public const string RetentionBelowLegalMinimum = "RP.Retention.BELOW_LEGAL_MINIMUM";
    public const string InvalidTemplateParameter = "RP.Template.INVALID_PARAMETER";
    public const string InvalidScheduleInterval = "RP.Schedule.INVALID_INTERVAL";
    public const string ExportDuplicateInProgress = "RP.Export.DUPLICATE_IN_PROGRESS";
    public const string ExportInvalidTransition = "RP.Export.INVALID_TRANSITION";
    public const string SavedReportNotOwner = "RP.SavedReport.NOT_OWNER";
    public const string InvalidDefinition = "RP.Definition.INVALID";
    public const string InvalidAlertRule = "RP.AlertRule.INVALID";
    public const string AccessDenied = "RP.Access.ADMIN_ONLY";
    public const string InsufficientData = "RP.Stats.INSUFFICIENT_DATA";
    public const string LaborMarketOnePerPeriod = "RP.LaborMarketReport.ONE_PER_PERIOD";
    public const string InvalidPeriod = "RP.LaborMarketReport.INVALID_PERIOD";
    public const string PowerBiUpstreamTimeout = "RP.PowerBI.UPSTREAM_TIMEOUT";
    public const string InvalidFact = "RP.Fact.INVALID";
}

internal static class Guard
{
    public static void Ensure(bool condition, string code, string message, string? externalCode = null, BusinessRuleKind kind = BusinessRuleKind.BusinessRule)
    {
        if (!condition)
        {
            throw new BusinessRuleViolationException(code, message, externalCode, kind);
        }
    }
}
