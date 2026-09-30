using JobPlatform.EmployerOnboarding.Application.DTOs.Standing;

namespace JobPlatform.EmployerOnboarding.Application.Interfaces;

/// <summary>Cache-aside port (foundation section 10); implemented in Infrastructure over ICacheStore. Failures degrade to the database.</summary>
public interface IEmployerCache
{
    Task<EmployerStandingView?> GetStandingAsync(Guid employerAccountId, CancellationToken ct = default);

    Task SetStandingAsync(Guid employerAccountId, EmployerStandingView view, CancellationToken ct = default);

    Task<CompanyPublicInfoView?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default);

    Task SetCompanyAsync(Guid employerAccountId, CompanyPublicInfoView view, CancellationToken ct = default);

    Task InvalidateAsync(Guid employerAccountId, CancellationToken ct = default);
}
