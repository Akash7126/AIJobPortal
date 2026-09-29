using FluentValidation;
using JobPlatform.Notification.Application.Queries.Preferences;

namespace JobPlatform.Notification.Application.Validators.Preferences;

public sealed class GetNotificationPreferencesValidator : AbstractValidator<GetNotificationPreferencesQuery>
{
    public GetNotificationPreferencesValidator() =>
        RuleFor(x => x.Channel).Must(c => c is "email" or "in-app" or "sms").WithErrorCode("VAL.Channel.Invalid");
}
