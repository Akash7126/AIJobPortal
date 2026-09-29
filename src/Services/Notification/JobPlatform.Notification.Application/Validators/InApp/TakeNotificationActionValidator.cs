using FluentValidation;
using JobPlatform.Notification.Application.Commands.InApp;

namespace JobPlatform.Notification.Application.Validators.InApp;

public sealed class TakeNotificationActionValidator : AbstractValidator<TakeNotificationActionCommand>
{
    public TakeNotificationActionValidator() => RuleFor(x => x.Id).NotEmpty().WithErrorCode("VAL.Id.Required");
}
