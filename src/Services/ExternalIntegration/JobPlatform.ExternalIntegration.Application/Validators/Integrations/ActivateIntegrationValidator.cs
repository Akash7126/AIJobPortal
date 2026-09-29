using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.Integrations;

namespace JobPlatform.ExternalIntegration.Application.Validators.Integrations;

public sealed class ActivateIntegrationValidator : AbstractValidator<ActivateIntegrationCommand>
{
    public ActivateIntegrationValidator() => RuleFor(c => c.IntegrationId).NotEmpty().WithErrorCode("VAL.IntegrationId.Required");
}
