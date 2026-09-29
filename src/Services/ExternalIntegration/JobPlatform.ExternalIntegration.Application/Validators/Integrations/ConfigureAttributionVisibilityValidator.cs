using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application.Validators.Integrations;

public sealed class ConfigureAttributionVisibilityValidator : AbstractValidator<ConfigureAttributionVisibilityCommand>
{
    public ConfigureAttributionVisibilityValidator() =>
        RuleFor(c => c.Visibility).Must(v => Enum.TryParse<AttributionVisibilityValue>(v, true, out _)).WithErrorCode("VAL.Visibility.Invalid");
}
