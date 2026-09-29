using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.Integrations;

namespace JobPlatform.ExternalIntegration.Application.Validators.Integrations;

public sealed class SuspendIntegrationValidator : AbstractValidator<SuspendIntegrationCommand>
{
    public SuspendIntegrationValidator()
    {
        RuleFor(c => c.IntegrationId).NotEmpty().WithErrorCode("VAL.IntegrationId.Required");
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500).WithErrorCode("VAL.Reason.Required");
    }
}
