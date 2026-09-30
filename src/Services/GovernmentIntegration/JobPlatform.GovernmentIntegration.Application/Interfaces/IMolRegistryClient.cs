using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Interfaces;

public interface IMolRegistryClient : IGovernmentVerificationSourceClient
{
    Task<EmployerVerificationCheckResult> VerifyEmployerAsync(Submission submission, CancellationToken ct);

    Task<SourceSyncResult> SyncAsync(CancellationToken ct);
}
