using FluentValidation;
using JobPlatform.Notification.Application.Commands.Preferences;
using JobPlatform.Notification.Domain;

namespace JobPlatform.Notification.Application.Validators.Preferences;

public sealed class SetEmailPreferenceValidator : AbstractValidator<SetEmailPreferenceCommand>
{
    public SetEmailPreferenceValidator()
    {
        RuleFor(x => x.Categories).NotNull().WithErrorCode("VAL.Categories.Required");
        RuleFor(x => x.Mode).Must(m => Enum.TryParse<DeliveryMode>(m, true, out _)).WithErrorCode("VAL.Mode.Invalid");
    }
}
