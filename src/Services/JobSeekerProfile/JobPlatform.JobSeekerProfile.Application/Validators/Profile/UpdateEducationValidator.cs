using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Profile;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Profile;

public sealed class UpdateEducationValidator : AbstractValidator<UpdateEducationCommand>
{
    public UpdateEducationValidator()
    {
        RuleForEach(c => c.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.Degree).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Degree.Invalid");
            e.RuleFor(x => x.Institution).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Institution.Invalid");
            e.RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithErrorCode("VAL.DateRange.Invalid");
        });
    }
}
