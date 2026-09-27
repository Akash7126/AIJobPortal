namespace JobPlatform.ExternalIntegration.Application;

// ---------------------------------------------------------------------- read models (foundation section 3.5: dedicated projections, never aggregates)

public sealed record SyncRunView(
    Guid RunId, string Trigger, string Status, DateTime StartedAtUtc, DateTime? EndedAtUtc, int MappingVersion, int Received, int Accepted,
    int Rejected, string? ErrorCode);

public sealed record IntegrationView(
    Guid IntegrationId, Guid PartnerAccountId, Guid SourcePlatformId, string SourcePlatformName, string BaseUrl, string AdmissionStatus,
    string Recommendation, string Status, bool PullEnabled, bool PushEnabled, string SyncMode, string? Cron, string AttributionVisibility,
    string Sandbox, IReadOnlyList<SyncRunView> SyncRuns, byte[] RowVersion);

public sealed record IntegrationSummaryView(
    Guid IntegrationId, Guid SourcePlatformId, string SourcePlatformName, string Status, string AttributionVisibility,
    IReadOnlyList<SyncRunView> RecentRuns);

public sealed record MappingRuleView(string SourceField, string TargetField, string Transform);

public sealed record JobDataMappingView(
    Guid MappingId, Guid IntegrationId, int Version, string StandardSchemaVersion, IReadOnlyList<MappingRuleView> Rules);

public sealed record StandardSchemaFieldView(string Field, bool Required, string Description);

public sealed record StandardSchemaView(string SchemaVersion, IReadOnlyList<StandardSchemaFieldView> Fields);

public sealed record ApiVersionView(
    string Version, string Status, DateTime? DeprecatedAtUtc, DateTime? SunsetAtUtc, IReadOnlyList<string> AcceptedFormats);

public sealed record ApiDocumentationView(string Version, string Content, bool Deprecated, DateTime? SunsetAtUtc);

public sealed record ApiSchemaDocumentationView(string Version, string SchemaJson, bool Deprecated, DateTime? SunsetAtUtc);

public sealed record SoftwareInterfaceView(Guid Id, string Category, string Name, string Endpoint, bool Enabled);

public sealed record PushJobDataResultView(string PlatformJobId, bool Created, string Confirmation);

public sealed record AttributionView(
    Guid AttributionId, string PlatformJobId, string SourcePlatformName, string SyncState, DateTime? DeadlineUtc, string? Description);
