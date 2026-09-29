using FluentValidation;
using JobPlatform.GovernmentIntegration.Application.Commands.EmployerVerifications;

namespace JobPlatform.GovernmentIntegration.Application.Validators.EmployerVerifications;

public sealed class RequestEmployerVerificationValidator : AbstractValidator<RequestEmployerVerificationCommand>
{
    public RequestEmployerVerificationValidator()
    {
        RuleFor(c => c.RegistrationNumber).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9\\-/]+$").WithErrorCode("VAL.RegistrationNumber.Invalid");
        RuleFor(c => c.VatNumber).NotEmpty().MaximumLength(50).WithErrorCode("VAL.VatNumber.Required");
        // Palestinian mobile ranges +970 59x/56x (proposed, handover section 7.3 "confirm"): kept permissive (E.164) beyond that prefix check.
        RuleFor(c => c.MobileNumber).NotEmpty().Matches("^\\+?[0-9]{7,15}$").WithErrorCode("VAL.MobileNumber.Invalid");
    }
}
