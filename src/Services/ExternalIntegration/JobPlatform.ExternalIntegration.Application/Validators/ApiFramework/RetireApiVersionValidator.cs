using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Validators.ApiFramework;

public sealed class RetireApiVersionValidator : AbstractValidator<RetireApiVersionCommand>
{
    public RetireApiVersionValidator() => RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
}
