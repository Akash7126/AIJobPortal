using JobPlatform.EmployerOnboarding.Application.DTOs.Standing;

namespace JobPlatform.EmployerOnboarding.Application.Queries.Standing;

public sealed record GetEmployerStandingQuery(Guid EmployerAccountId) : ServiceQuery<EmployerStandingView>;
