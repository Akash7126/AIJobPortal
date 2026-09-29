using FluentValidation;
using JobPlatform.CandidateSourcing.Application.Commands.Threshold;

namespace JobPlatform.CandidateSourcing.Application.Validators.Threshold;

public sealed class SetQualificationThresholdValidator : AbstractValidator<SetQualificationThresholdCommand>
{
    public SetQualificationThresholdValidator()
    {
        RuleFor(c => c.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(c => c.Percent).InclusiveBetween(0, 100).WithErrorCode("VAL.Percent.OutOfRange");
    }
}
