using FluentValidation;
using JobPlatform.PlatformAdministration.Application.Commands.Offerings;
using JobPlatform.PlatformAdministration.Application.Validators.Common;

namespace JobPlatform.PlatformAdministration.Application.Validators.Offerings;

public sealed class SuspendJobOfferingValidator : AbstractValidator<SuspendJobOfferingCommand>
{
    public SuspendJobOfferingValidator()
    {
        RuleFor(c => c.JobOfferingId).NotEmpty().WithErrorCode("VAL.JobOfferingId.Required");
        RuleFor(c => c.Reason).ValidModerationReason();
    }
}
