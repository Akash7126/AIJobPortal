namespace JobPlatform.ExternalIntegration.Application;

public sealed record AttributionView(
    Guid AttributionId, string PlatformJobId, string SourcePlatformName, string SyncState, DateTime? DeadlineUtc, string? Description);
