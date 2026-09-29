using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;

namespace JobPlatform.ExternalIntegration.Application.Validators.ApiFramework;

public sealed class DeprecateApiVersionValidator : AbstractValidator<DeprecateApiVersionCommand>
{
    public static readonly TimeSpan MinimumWindow = TimeSpan.FromDays(90);

    public DeprecateApiVersionValidator()
    {
        RuleFor(c => c.Version).Matches(@"^v\d+$").WithErrorCode("VAL.Version.InvalidFormat");
        RuleFor(c => c.SunsetAtUtc).GreaterThanOrEqualTo(_ => DateTime.UtcNow + MinimumWindow).WithErrorCode("VAL.SunsetAtUtc.TooSoon");
    }
}
