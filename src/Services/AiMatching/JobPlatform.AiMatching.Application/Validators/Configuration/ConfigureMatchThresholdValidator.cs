using FluentValidation;
using JobPlatform.AiMatching.Application.Commands.Configuration;

namespace JobPlatform.AiMatching.Application.Validators.Configuration;

/// <summary>Malformed input is a 400; the domain re-checks the business bounds.</summary>
public sealed class ConfigureMatchThresholdValidator : AbstractValidator<ConfigureMatchThresholdCommand>
{
    public ConfigureMatchThresholdValidator() =>
        RuleFor(x => x.ThresholdPercent).InclusiveBetween(0m, 100m).WithErrorCode("VAL.ThresholdPercent.OutOfRange");
}
