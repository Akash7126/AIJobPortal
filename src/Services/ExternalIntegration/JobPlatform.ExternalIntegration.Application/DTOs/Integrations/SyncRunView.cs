namespace JobPlatform.ExternalIntegration.Application.DTOs.Integrations;

public sealed record SyncRunView(
    Guid RunId, string Trigger, string Status, DateTime StartedAtUtc, DateTime? EndedAtUtc, int MappingVersion, int Received, int Accepted,
    int Rejected, string? ErrorCode);
