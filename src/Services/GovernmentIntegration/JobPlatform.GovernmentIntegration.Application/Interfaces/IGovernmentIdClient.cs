using JobPlatform.GovernmentIntegration.Domain;

namespace JobPlatform.GovernmentIntegration.Application.Interfaces;

public interface IGovernmentIdClient
{
    Task<IdentityCheckResult> CheckAsync(IdentityClaim claim, CancellationToken ct);
}
