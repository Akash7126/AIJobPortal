namespace JobPlatform.ExternalIntegration.Application.DTOs.Integrations;

public sealed record IntegrationView(
    Guid IntegrationId, Guid PartnerAccountId, Guid SourcePlatformId, string SourcePlatformName, string BaseUrl, string AdmissionStatus,
    string Recommendation, string Status, bool PullEnabled, bool PushEnabled, string SyncMode, string? Cron, string AttributionVisibility,
    string Sandbox, IReadOnlyList<SyncRunView> SyncRuns, byte[] RowVersion);
