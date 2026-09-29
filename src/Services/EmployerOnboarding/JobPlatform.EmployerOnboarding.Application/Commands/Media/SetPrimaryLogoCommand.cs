using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Commands.Media;

public sealed record SetPrimaryLogoCommand(Guid CompanyMediaId) : EmployerCommand<Unit>;
