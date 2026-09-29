using FluentValidation;
using JobPlatform.GovernmentIntegration.Application.Commands.EmployerVerifications;

namespace JobPlatform.GovernmentIntegration.Application.Validators.EmployerVerifications;

public sealed class DecideEmployerVerificationManuallyValidator : AbstractValidator<DecideEmployerVerificationManuallyCommand>
{
    public DecideEmployerVerificationManuallyValidator()
    {
        RuleFor(c => c.EmployerVerificationId).NotEmpty().WithErrorCode("VAL.EmployerVerificationId.Required");
        RuleFor(c => c.Decision).IsInEnum().WithErrorCode("VAL.Decision.Invalid");
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500).When(c => c.Decision == ManualDecision.Reject).WithErrorCode("VAL.Reason.Required");
    }
}
