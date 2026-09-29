namespace JobPlatform.ExternalIntegration.Application.DTOs.Integrations;

public sealed record IntegrationSummaryView(
    Guid IntegrationId, Guid SourcePlatformId, string SourcePlatformName, string Status, string AttributionVisibility,
    IReadOnlyList<SyncRunView> RecentRuns);
