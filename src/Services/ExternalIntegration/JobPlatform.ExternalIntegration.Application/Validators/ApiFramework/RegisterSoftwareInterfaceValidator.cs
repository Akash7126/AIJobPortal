using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application.Validators.ApiFramework;

public sealed class RegisterSoftwareInterfaceValidator : AbstractValidator<RegisterSoftwareInterfaceCommand>
{
    public RegisterSoftwareInterfaceValidator()
    {
        RuleFor(c => c.Category).Must(v => Enum.TryParse<SoftwareInterfaceCategory>(v, true, out _)).WithErrorCode("VAL.Category.Invalid");
        RuleFor(c => c.Name).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Name.Required");
        RuleFor(c => c.Endpoint).Must(BeAnAbsoluteHttpsUrl).WithErrorCode("VAL.Endpoint.Invalid");
    }

    private static bool BeAnAbsoluteHttpsUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
