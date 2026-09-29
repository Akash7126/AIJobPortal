using FluentValidation;
using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.Validators.Postings;

public sealed class CreateJobPostingValidator : AbstractValidator<CreateJobPostingCommand>
{
    public CreateJobPostingValidator()
    {
        SharedFieldRules.Apply(this, c => c.TitleAr, c => c.TitleEn, c => c.SummaryAr, c => c.SummaryEn, c => c.Skills, c => c.JobLink, c => c.DeadlineUtc,
            c => c.RequiredLanguages, c => c.SalaryMin, c => c.SalaryMax, c => c.OtherFields);
        RuleFor(c => c.ContractType).IsEnumName(typeof(ContractType), false).WithErrorCode("VAL.ContractType.Invalid");
        RuleFor(c => c.WorkFormat).IsEnumName(typeof(WorkFormat), false).WithErrorCode("VAL.WorkFormat.Invalid");
        RuleFor(c => c.EducationLevel).Must(v => v is null || Enum.TryParse<EducationLevel>(v, true, out _)).WithErrorCode("VAL.EducationLevel.Invalid");
        RuleFor(c => c.DeadlineUtc).GreaterThan(DateTime.UtcNow).WithErrorCode("VAL.Deadline.MustBeFuture");
    }
}
