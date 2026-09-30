using JobPlatform.EmployerOnboarding.Application.DTOs.Media;
using JobPlatform.EmployerOnboarding.Application.DTOs.Registration;
using JobPlatform.EmployerOnboarding.Application.DTOs.Standing;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.EmployerOnboarding.Application.Interfaces;

/// <summary>Read side (foundation section 3.5): dedicated projections, never aggregates.</summary>
public interface IEmployerReadStore
{
    Task<EmployerRegistrationView?> GetRegistrationAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<PagedResult<EmployerRegistrationView>> ListRegistrationsAsync(string? status, PageRequest page, CancellationToken ct = default);

    Task<IReadOnlyList<CompanyMediaView>> ListMediaAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<EmployerStandingView?> GetStandingAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<CompanyPublicInfoView?> GetCompanyPublicInfoAsync(Guid employerAccountId, CancellationToken ct = default);
}
