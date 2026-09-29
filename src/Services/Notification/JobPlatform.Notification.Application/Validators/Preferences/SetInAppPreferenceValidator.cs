using FluentValidation;
using JobPlatform.Notification.Application.Commands.Preferences;

namespace JobPlatform.Notification.Application.Validators.Preferences;

public sealed class SetInAppPreferenceValidator : AbstractValidator<SetInAppPreferenceCommand>
{
    public SetInAppPreferenceValidator() => RuleFor(x => x.Categories).NotNull().WithErrorCode("VAL.Categories.Required");
}
