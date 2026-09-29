using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.Integrations;

namespace JobPlatform.ExternalIntegration.Application.Validators.Integrations;

public sealed class RegisterExternalJobSiteValidator : AbstractValidator<RegisterExternalJobSiteCommand>
{
    public RegisterExternalJobSiteValidator()
    {
        RuleFor(c => c.SourcePlatformName).NotEmpty().MaximumLength(200).WithErrorCode("VAL.SourcePlatformName.Required");
        RuleFor(c => c.BaseUrl).NotEmpty().Must(BeAnAbsoluteHttpsUrl).WithErrorCode("VAL.BaseUrl.Invalid");
    }

    private static bool BeAnAbsoluteHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
