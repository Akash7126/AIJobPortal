using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Profile;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Profile;

public sealed class UpdateExperienceValidator : AbstractValidator<UpdateExperienceCommand>
{
    public UpdateExperienceValidator()
    {
        RuleForEach(c => c.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.Company).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Company.Invalid");
            e.RuleFor(x => x.Role).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Role.Invalid");
            e.RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From <= x.To).WithErrorCode("VAL.DateRange.Invalid");
        });
        RuleFor(c => c.YearsOfExperience).GreaterThanOrEqualTo(0).When(c => c.YearsOfExperience is not null).WithErrorCode("VAL.YearsOfExperience.Invalid");
    }
}
