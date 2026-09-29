using FluentValidation;
using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.Validators.Postings;

public sealed class UpdateJobPostingStatusValidator : AbstractValidator<UpdateJobPostingStatusCommand>
{
    public UpdateJobPostingStatusValidator()
    {
        RuleFor(c => c.Status).IsEnumName(typeof(JobPostingStatus), false).WithErrorCode("VAL.Status.Invalid");
    }
}
