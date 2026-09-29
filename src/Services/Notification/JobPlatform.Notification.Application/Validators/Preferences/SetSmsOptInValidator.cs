using FluentValidation;
using JobPlatform.Notification.Application.Commands.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.Notification.Application.Validators.Preferences;

public sealed class SetSmsOptInValidator : AbstractValidator<SetSmsOptInCommand>
{
    public SetSmsOptInValidator() =>
        When(x => x.OptIn, () => RuleFor(x => x.Mobile).Must(m => MobileNumber.TryCreate(m, out _)).WithErrorCode(NotificationErrorCodes.SmsInvalidField));
}
