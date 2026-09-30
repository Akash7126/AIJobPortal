namespace JobPlatform.GovernmentIntegration.Application.Interfaces;

public interface IPefClient : IGovernmentVerificationSourceClient
{
    Task<SourceSyncResult> SyncAsync(CancellationToken ct);
}
