namespace JobPlatform.ExternalIntegration.Domain;

public enum IntegrationStatus
{
    Registered,
    Active,
    Suspended
}

public enum AdmissionRecommendation
{
    Recommended,
    NotRecommended
}

public enum AdmissionStatus
{
    UnderReview,
    Recommended,
    Approved,
    Rejected
}

public enum SyncMode
{
    Scheduled,
    OnDemand
}

public enum AttributionVisibilityValue
{
    Public,
    AdminOnly
}

public enum SandboxState
{
    NotProvisioned,
    Provisioned
}

public enum SyncTrigger
{
    Scheduled,
    OnDemand
}

public enum SyncRunStatus
{
    Running,
    Completed,
    Failed
}

public enum JobDataStatus
{
    Received,
    Standardized,
    Accepted,
    Rejected
}

public enum JobDataModel
{
    Push,
    Pull
}

public enum AttributionSyncState
{
    Active,
    Closed,
    Deactivated,
    Deleted
}

public enum MappingTransform
{
    None,
    Trim,
    ToUpper,
    ToLower,
    SplitComma
}

public enum ApiVersionStatus
{
    Active,
    Deprecated,
    Retired
}

public enum SoftwareInterfaceCategory
{
    ExternalJobSite,
    GovernmentDatabase,
    EmailSmsGateway,
    AnalyticsReporting
}

public enum AttributionOperation
{
    ExtendDeadline,
    EditDescription,
    Close,
    Deactivate,
    Delete
}
