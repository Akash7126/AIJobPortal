using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Commands.Media;

public sealed record RemoveCompanyMediaCommand(Guid CompanyMediaId) : EmployerCommand<Unit>;
