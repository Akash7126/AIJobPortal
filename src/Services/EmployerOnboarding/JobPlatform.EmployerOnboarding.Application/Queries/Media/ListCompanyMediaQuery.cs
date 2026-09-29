using JobPlatform.EmployerOnboarding.Application.DTOs.Media;

namespace JobPlatform.EmployerOnboarding.Application.Queries.Media;

public sealed record ListCompanyMediaQuery : EmployerQuery<IReadOnlyList<CompanyMediaView>>;
