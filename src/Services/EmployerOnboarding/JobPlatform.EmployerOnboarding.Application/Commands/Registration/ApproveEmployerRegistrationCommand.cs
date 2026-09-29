using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.EmployerOnboarding.Application.Commands.Registration;

public sealed record ApproveEmployerRegistrationCommand(Guid EmployerRegistrationId) : AdminCommand<Unit>;
