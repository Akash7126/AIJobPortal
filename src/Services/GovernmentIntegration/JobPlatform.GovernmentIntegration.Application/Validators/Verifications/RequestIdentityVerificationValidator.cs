using FluentValidation;
using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;

namespace JobPlatform.GovernmentIntegration.Application.Validators.Verifications;

public sealed class RequestIdentityVerificationValidator : AbstractValidator<RequestIdentityVerificationCommand>
{
    public RequestIdentityVerificationValidator()
    {
        RuleFor(c => c.SubjectId).NotEmpty().WithErrorCode("VAL.SubjectId.Required");
        RuleFor(c => c.NationalIdReference).NotEmpty().MaximumLength(64).WithErrorCode("VAL.NationalIdReference.Required");
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200).WithErrorCode("VAL.FullName.Required");
        RuleFor(c => c.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithErrorCode("VAL.DateOfBirth.MustBeInPast");
    }
}
