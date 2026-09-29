using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Preferences;
using JobPlatform.JobSeekerProfile.Domain;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Preferences;

public sealed class ConfigureJobPreferenceValidator : AbstractValidator<ConfigureJobPreferenceCommand>
{
    public ConfigureJobPreferenceValidator()
    {
        RuleFor(c => c.JobTypes).Must(l => l.Count <= 20).WithErrorCode("VAL.JobTypes.TooMany");
        RuleFor(c => c.Industries).Must(l => l.Count <= 20).WithErrorCode("VAL.Industries.TooMany");
        RuleFor(c => c.Locations).Must(l => l.Count <= 20).WithErrorCode("VAL.Locations.TooMany");
        RuleFor(c => c).Must(c => c.SalaryMin is null || c.SalaryMax is null || c.SalaryMin <= c.SalaryMax).WithErrorCode("VAL.SalaryRange.Invalid");
        RuleForEach(c => c.WorkArrangements).Must(w => Enum.TryParse<WorkArrangement>(w, true, out _)).WithErrorCode("VAL.WorkArrangement.Invalid");
    }
}
