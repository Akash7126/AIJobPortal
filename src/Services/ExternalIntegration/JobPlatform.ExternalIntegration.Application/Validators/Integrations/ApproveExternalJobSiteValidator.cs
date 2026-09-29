using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.Integrations;

namespace JobPlatform.ExternalIntegration.Application.Validators.Integrations;

public sealed class ApproveExternalJobSiteValidator : AbstractValidator<ApproveExternalJobSiteCommand>
{
    public ApproveExternalJobSiteValidator()
    {
        RuleFor(c => c.IntegrationId).NotEmpty().WithErrorCode("VAL.IntegrationId.Required");
        RuleFor(c => c.ApprovalBasis).NotEmpty().MaximumLength(500).WithErrorCode("VAL.ApprovalBasis.Required");
    }
}
