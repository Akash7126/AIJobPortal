using FluentValidation;
using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;

namespace JobPlatform.GovernmentIntegration.Application.Validators.Verifications;

public sealed class RequestGovernmentVerificationValidator : AbstractValidator<RequestGovernmentVerificationCommand>
{
    public RequestGovernmentVerificationValidator()
    {
        RuleFor(c => c.RequestingComponent).NotEmpty().WithErrorCode("VAL.RequestingComponent.Required");
        RuleFor(c => c.SubjectType).IsInEnum().WithErrorCode("VAL.SubjectType.Invalid");
        RuleFor(c => c.SubjectId).NotEmpty().WithErrorCode("VAL.SubjectId.Required");
        RuleFor(c => c.Source).IsInEnum().WithErrorCode("VAL.Source.Invalid");
        RuleFor(c => c.Purpose).IsInEnum().WithErrorCode("VAL.Purpose.Invalid");
    }
}
