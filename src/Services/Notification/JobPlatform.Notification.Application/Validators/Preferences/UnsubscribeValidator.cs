using FluentValidation;
using JobPlatform.Notification.Application.Commands.Preferences;

namespace JobPlatform.Notification.Application.Validators.Preferences;

public sealed class UnsubscribeValidator : AbstractValidator<UnsubscribeCommand>
{
    public UnsubscribeValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(500).WithErrorCode("VAL.Token.Required");
}
