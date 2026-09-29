using FluentValidation;
using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;

namespace JobPlatform.GovernmentIntegration.Application.Validators.Verifications;

public sealed class RequestEducationalCredentialVerificationValidator : AbstractValidator<RequestEducationalCredentialVerificationCommand>
{
    public RequestEducationalCredentialVerificationValidator()
    {
        RuleFor(c => c.SubjectId).NotEmpty().WithErrorCode("VAL.SubjectId.Required");
        RuleFor(c => c.Institution).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Institution.Required");
        RuleFor(c => c.CredentialName).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Credential.Required");
        RuleFor(c => c.Year).InclusiveBetween(1950, DateTime.UtcNow.Year).WithErrorCode("VAL.Year.OutOfRange");
    }
}
