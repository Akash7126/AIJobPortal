using FluentValidation;
using JobPlatform.AiMatching.Application.Commands.Configuration;
using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.Validators.Configuration;

/// <summary>Malformed input is a 400; the domain re-checks the business bounds.</summary>
public sealed class ConfigureMatchingParameterValidator : AbstractValidator<ConfigureMatchingParameterCommand>
{
    public ConfigureMatchingParameterValidator()
    {
        RuleFor(x => x.SkillOverlap).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Education).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Training).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Location).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Experience).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x.Salary).InclusiveBetween(0m, 100m).WithErrorCode("VAL.Weight.OutOfRange");
        RuleFor(x => x).Must(x => Math.Abs(x.SkillOverlap + x.Education + x.Training + x.Location + x.Experience + x.Salary - 100m) <= CriterionWeights.Epsilon)
            .OverridePropertyName("weights").WithErrorCode("VAL.Weights.SumNot100");
    }
}
