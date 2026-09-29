using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Validators.ApiFramework;

public sealed class ViewApiSchemaDocumentationValidator : AbstractValidator<ViewApiSchemaDocumentationCommand>
{
    public ViewApiSchemaDocumentationValidator() => RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
}
