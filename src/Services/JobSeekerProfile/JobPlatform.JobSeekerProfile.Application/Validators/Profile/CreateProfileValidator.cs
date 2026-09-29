using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Profile;
using JobPlatform.JobSeekerProfile.Application.Validators.Common;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Profile;

public sealed class CreateProfileValidator : AbstractValidator<CreateProfileCommand>
{
    public CreateProfileValidator()
    {
        RuleFor(c => c.FullName).ValidFullName();
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.MobileNumber).ValidMobile();
        RuleFor(c => c.Gender).ValidGender();
    }
}
