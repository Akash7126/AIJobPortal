using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Domain;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Profile;

public sealed class UpdateSkillsValidator : AbstractValidator<UpdateSkillsCommand>
{
    public UpdateSkillsValidator()
    {
        RuleFor(c => c.Entries).Must(e => e.Count <= 100).WithErrorCode("VAL.Skills.TooMany");
        RuleForEach(c => c.Entries).ChildRules(e =>
        {
            e.RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithErrorCode("VAL.Skill.Invalid");
            e.RuleFor(x => x.Kind).Must(k => Enum.TryParse<SkillKind>(k, true, out _)).WithErrorCode("VAL.SkillKind.Invalid");
            e.RuleFor(x => x.Class).Must(k => Enum.TryParse<SkillClass>(k, true, out _)).WithErrorCode("VAL.SkillClass.Invalid");
        });
    }
}
