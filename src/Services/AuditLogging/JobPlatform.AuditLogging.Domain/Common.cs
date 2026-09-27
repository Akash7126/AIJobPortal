using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AuditLogging.Domain;

public enum AuditCategory
{
    Access,
    AdminAction,
    ApiCall,
    Submission,
    SyncError,
    JobAudit,
    GovernmentExchange,
    JobStatus,
    Email,
    Sms,
    Notification,
    Insight,
    Profile
}

public enum AuditOutcome
{
    Success,
    Failure,
    Duplicate,
    Denied
}

/// <summary>Who may see an entry: the owner (partner / employer / user) or administrators only.</summary>
public enum OwnerType
{
    AdminOnly,
    Partner,
    Employer,
    User
}

public enum SyncStatus
{
    Pending,
    Synced,
    Failed,
    Archived
}

public enum ExportStatus
{
    Queued,
    Generating,
    Ready,
    Failed
}

public enum ExportFormat
{
    Csv,
    Json,
    Xml
}

public enum ReportType
{
    PostingsBySector,
    PostingsByRegion,
    PostingsByIndustry,
    UserRegistrations,
    TopSearches,
    UserInteractions,
    SystemMetrics
}

/// <summary>Externally published error codes (handover sections 4 and 6).</summary>
public static class AuditErrorCodes
{
    public const string PartnerForbidden = "E-TPJPRI-FORBIDDEN";
    public const string PartnerInvalidField = "E-TPJPRI-INVALID-FIELD";
    public const string AdminForbidden = "E-AUM-FORBIDDEN";
    public const string AccessForbidden = "E-AAFR-FORBIDDEN";
    public const string GovernmentForbidden = "E-GDI-FORBIDDEN";
    public const string EmailForbidden = "E-EMAILN-FORBIDDEN";
    public const string SmsForbidden = "E-SMSN-FORBIDDEN";
    public const string NotificationForbidden = "E-INAPPN-FORBIDDEN";
    public const string JobStatusForbidden = "E-JST-FORBIDDEN";
    public const string EmployerForbidden = "E-AUDIT-EMPLOYER-FORBIDDEN";
    public const string InsightForbidden = "E-AUDIT-INSIGHT-FORBIDDEN";
    public const string ExportNotFound = "E-AUDIT-EXPORT-NOT-FOUND";
    public const string InsightNotFound = "E-AUDIT-INSIGHT-NOT-FOUND";
    public const string InvalidField = "E-AUDIT-INVALID-FIELD";
}

public static class AuditRuleCodes
{
    public const string DuplicateSourceMessage = "AL.Entry.DUPLICATE_SOURCE_MESSAGE";
    public const string PiiInDetails = "AL.Entry.NO_PII_IN_DETAILS";
    public const string InvalidEntry = "AL.Entry.INVALID";
    public const string NotOwner = "AL.Scope.NOT_OWNER";
    public const string AdminOnly = "AL.Scope.ADMIN_ONLY";
    public const string InvalidDateRange = "AL.Usage.INVALID_DATE_RANGE";
    public const string RetryViaPending = "AL.Sync.RETRY_VIA_PENDING";
    public const string DuplicateExportInProgress = "AL.Export.DUPLICATE_IN_PROGRESS";
    public const string ExportInvalidTransition = "AL.Export.INVALID_TRANSITION";
    public const string RetentionInvalid = "AL.Retention.INVALID";
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
