using JobPlatform.HelpContent.Application.DTOs.CompanyPage;

namespace JobPlatform.HelpContent.Application.Interfaces;

/// <summary>Cache-aside port (foundation section 10, handover section 9); implemented in Infrastructure over ICacheStore.</summary>
public interface IHelpContentCache
{
    Task<CompanyPageView?> GetCompanyPageAsync(Guid employerAccountId, CancellationToken ct = default);

    Task SetCompanyPageAsync(Guid employerAccountId, CompanyPageView view, CancellationToken ct = default);

    Task InvalidateCompanyPageAsync(Guid employerAccountId, CancellationToken ct = default);
}
