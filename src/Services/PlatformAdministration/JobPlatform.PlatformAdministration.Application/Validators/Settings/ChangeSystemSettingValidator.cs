using FluentValidation;
using JobPlatform.PlatformAdministration.Application.Commands.Settings;
using JobPlatform.PlatformAdministration.Domain.Settings;

namespace JobPlatform.PlatformAdministration.Application.Validators.Settings;

public sealed class ChangeSystemSettingValidator : AbstractValidator<ChangeSystemSettingCommand>
{
    public ChangeSystemSettingValidator()
    {
        RuleFor(c => c.Key).NotEmpty().WithErrorCode("VAL.Key.Required")
            .Must(key => SystemSettingCatalog.Find(key) is not null).WithErrorCode("VAL.Key.Unknown");
        RuleFor(c => c.Value).NotNull().WithErrorCode("VAL.Value.Required");
        // The value must parse to the setting's type; the bounds are a domain rule (INV-08).
        RuleFor(c => c.Value).Must((command, value) =>
                SystemSettingCatalog.Find(command.Key) is not { } definition || SettingValueParser.CanParse(definition.ValueType, value))
            .WithErrorCode("VAL.Value.InvalidType").When(c => c.Value is not null);
    }
}
