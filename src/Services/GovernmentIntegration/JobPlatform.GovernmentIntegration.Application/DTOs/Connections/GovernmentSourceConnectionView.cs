namespace JobPlatform.GovernmentIntegration.Application.DTOs.Connections;

public sealed record GovernmentSourceConnectionView(
    Guid Id, string Source, string Endpoint, string AuthMethod, bool Enabled, string Health, DateTime? LastSuccessfulSyncAtUtc);
