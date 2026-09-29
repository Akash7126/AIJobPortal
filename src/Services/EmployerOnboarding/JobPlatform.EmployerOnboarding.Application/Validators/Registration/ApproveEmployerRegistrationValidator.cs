using FluentValidation;
using JobPlatform.EmployerOnboarding.Application.Commands.Registration;

namespace JobPlatform.EmployerOnboarding.Application.Validators.Registration;

public sealed class ApproveEmployerRegistrationValidator : AbstractValidator<ApproveEmployerRegistrationCommand>
{
    public ApproveEmployerRegistrationValidator() => RuleFor(c => c.EmployerRegistrationId).NotEmpty().WithErrorCode("VAL.EmployerRegistrationId.Required");
}
