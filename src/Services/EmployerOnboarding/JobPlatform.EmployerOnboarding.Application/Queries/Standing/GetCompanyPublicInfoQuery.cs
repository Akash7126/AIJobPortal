using JobPlatform.EmployerOnboarding.Application.DTOs.Standing;

namespace JobPlatform.EmployerOnboarding.Application.Queries.Standing;

public sealed record GetCompanyPublicInfoQuery(Guid EmployerAccountId) : ServiceQuery<CompanyPublicInfoView>;
