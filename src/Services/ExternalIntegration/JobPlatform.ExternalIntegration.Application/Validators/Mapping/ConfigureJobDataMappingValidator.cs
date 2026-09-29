using FluentValidation;
using JobPlatform.ExternalIntegration.Application.Commands.Mapping;
using JobPlatform.ExternalIntegration.Domain;

namespace JobPlatform.ExternalIntegration.Application.Validators.Mapping;

public sealed class ConfigureJobDataMappingValidator : AbstractValidator<ConfigureJobDataMappingCommand>
{
    private static readonly string[] AllowedTransforms = Enum.GetNames<MappingTransform>();

    public ConfigureJobDataMappingValidator()
    {
        RuleFor(c => c.Rules).NotEmpty().WithErrorCode("VAL.Rules.Required");
        RuleForEach(c => c.Rules).ChildRules(rule =>
        {
            rule.RuleFor(r => r.SourceField).NotEmpty().WithErrorCode("VAL.SourceField.Required");
            rule.RuleFor(r => r.TargetField).NotEmpty().WithErrorCode("VAL.TargetField.Required");
            rule.RuleFor(r => r.Transform).Must(t => AllowedTransforms.Contains(t, StringComparer.OrdinalIgnoreCase))
                .WithErrorCode("VAL.Transform.Invalid");
        });
        RuleFor(c => c.Rules).Must(rules => rules.Select(r => r.TargetField.ToLowerInvariant()).Distinct().Count() == rules.Count)
            .WithErrorCode("VAL.Rules.DuplicateTargetField");
        RuleFor(c => c.StandardSchemaVersion).NotEmpty().WithErrorCode("VAL.StandardSchemaVersion.Required");
    }
}
