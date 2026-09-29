using JobPlatform.HelpContent.Application.DTOs.CompanyPage;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.HelpContent.Application.Queries.CompanyPage;

/// <summary>
/// US-3.1.2-05: the composed company profile page - company info/verification badge from BC-05, current job openings from BC-09 (Q-05,
/// resolved: BC-09 now exposes GET /internal/v1/employers/{id}/open-postings), and the page background this BC owns. Cache-aside 5 min
/// (handover section 9), invalidated by CompanyPageChangedDomainEvent. Unverified employer shows no badge (AC-02); BC-09 unavailable
/// degrades to an empty postings list with a notice flag rather than failing the whole page (AC-04).
/// </summary>
public sealed record GetCompanyProfilePageQuery(Guid EmployerAccountId) : IQuery<CompanyPageView>;
