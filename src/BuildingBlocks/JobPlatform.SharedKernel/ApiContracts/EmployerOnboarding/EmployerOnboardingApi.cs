namespace JobPlatform.SharedKernel.ApiContracts.EmployerOnboarding;

/// <summary>
/// Synchronous contract of BC-05 Employer Onboarding (routes under /internal/v1). Consumers: BC-09 and BC-11 (verified-employer gate).
/// </summary>
public interface IEmployerOnboardingApi
{
    /// <summary>GET /internal/v1/employers/{accountId}/standing. Null = 404.</summary>
    Task<EmployerStandingDto?> GetEmployerStandingAsync(Guid employerAccountId, CancellationToken ct = default);
}

public sealed record EmployerStandingDto(Guid EmployerAccountId, bool Approved, bool Verified, string? Badge);
