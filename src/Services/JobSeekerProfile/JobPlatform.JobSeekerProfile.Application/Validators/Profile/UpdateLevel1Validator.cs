using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.Validators.Common;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Profile;

public sealed class UpdateLevel1Validator : AbstractValidator<UpdateLevel1Command>
{
    public UpdateLevel1Validator()
    {
        RuleFor(c => c.FullName).ValidFullName();
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.MobileNumber).ValidMobile();
        RuleFor(c => c.Gender).ValidGender();
    }
}
