using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Administration;

namespace JobPlatform.AccountIdentity.Application.Validators.Administration;

public sealed class ConfigurePasswordPolicyValidator : AbstractValidator<ConfigurePasswordPolicyCommand>
{
    public ConfigurePasswordPolicyValidator()
    {
        RuleFor(x => x.MinLength).InclusiveBetween(8, 128).WithErrorCode("VAL.MinLength.Range");
        RuleFor(x => x).Must(x => x.RequireUpper || x.RequireLower || x.RequireDigit).OverridePropertyName("CharacterClasses")
            .WithErrorCode("VAL.CharacterClasses.Required");
    }
}
