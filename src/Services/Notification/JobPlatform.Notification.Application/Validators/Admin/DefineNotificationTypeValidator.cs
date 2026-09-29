using FluentValidation;
using JobPlatform.Notification.Application.Commands.Admin;
using JobPlatform.Notification.Domain;

namespace JobPlatform.Notification.Application.Validators.Admin;

public sealed class DefineNotificationTypeValidator : AbstractValidator<DefineNotificationTypeCommand>
{
    public DefineNotificationTypeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).WithErrorCode("VAL.Code.Required");
        RuleFor(x => x.Icon).Must(i => NotificationType.AllowedIcons.Contains(i)).WithErrorCode("VAL.Icon.NotAllowed");
        RuleFor(x => x.Colour).Must(c => Contrast.OnWhite(c) >= 4.5).WithErrorCode("VAL.Colour.LowContrast");
        RuleFor(x => x.TextAlternative).NotEmpty().MaximumLength(200).WithErrorCode("VAL.TextAlternative.Required");
    }
}
