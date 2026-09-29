using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Validators.ApiFramework;

public sealed class ConfigureApiDataFormatValidator : AbstractValidator<ConfigureApiDataFormatCommand>
{
    public ConfigureApiDataFormatValidator()
    {
        RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
        RuleFor(c => c.Formats).NotEmpty().WithErrorCode("VAL.Formats.Required");
    }
}
