using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Validators.ApiFramework;

public sealed class ReleaseApiVersionValidator : AbstractValidator<ReleaseApiVersionCommand>
{
    public ReleaseApiVersionValidator() => RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
}
