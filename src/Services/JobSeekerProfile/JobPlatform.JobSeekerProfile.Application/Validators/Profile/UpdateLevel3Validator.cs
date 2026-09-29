using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Commands.Profile;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Profile;

public sealed class UpdateLevel3Validator : AbstractValidator<UpdateLevel3Command>
{
    public UpdateLevel3Validator()
    {
        RuleFor(c => c.Statement).MaximumLength(1000).WithErrorCode("VAL.Statement.TooLong");
        RuleFor(c => c.Bio).MaximumLength(2000).WithErrorCode("VAL.Bio.TooLong");
        RuleForEach(c => c.SocialLinks).ChildRules(l =>
        {
            l.RuleFor(x => x.Network).NotEmpty().WithErrorCode("VAL.SocialLink.NetworkRequired");
            l.RuleFor(x => x.Url).NotEmpty().Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                .WithErrorCode("VAL.SocialLink.UrlInvalid");
        });
    }
}
