using FluentValidation;
using JobPlatform.CandidateSourcing.Application.Commands.TalentPool;

namespace JobPlatform.CandidateSourcing.Application.Validators.TalentPool;

public sealed class AddToTalentPoolValidator : AbstractValidator<AddToTalentPoolCommand>
{
    public AddToTalentPoolValidator()
    {
        RuleFor(c => c.CandidateProfileId).NotEmpty().WithErrorCode("VAL.CandidateProfileId.Required");
        RuleFor(c => c.JobPostingId).NotEmpty().WithErrorCode("VAL.JobPostingId.Required");
        RuleFor(c => c.Note).MaximumLength(500).WithErrorCode("VAL.Note.TooLong");
    }
}
