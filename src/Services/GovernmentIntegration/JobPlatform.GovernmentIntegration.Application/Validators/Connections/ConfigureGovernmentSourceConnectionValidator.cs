using FluentValidation;
using JobPlatform.GovernmentIntegration.Application.Commands.Connections;

namespace JobPlatform.GovernmentIntegration.Application.Validators.Connections;

public sealed class ConfigureGovernmentSourceConnectionValidator : AbstractValidator<ConfigureGovernmentSourceConnectionCommand>
{
    public ConfigureGovernmentSourceConnectionValidator()
    {
        RuleFor(c => c.Source).IsInEnum().WithErrorCode("VAL.Source.Invalid");
        RuleFor(c => c.Endpoint).NotEmpty().Must(BeAnAbsoluteHttpsUrl).WithErrorCode("VAL.Endpoint.MustBeHttps");
        RuleFor(c => c.CredentialRef).NotEmpty().WithErrorCode("VAL.CredentialRef.Required");
    }

    private static bool BeAnAbsoluteHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
