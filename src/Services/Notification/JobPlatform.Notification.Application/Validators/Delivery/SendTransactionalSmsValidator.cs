using FluentValidation;
using JobPlatform.Notification.Application.Commands.Delivery;
using JobPlatform.Notification.Domain;

namespace JobPlatform.Notification.Application.Validators.Delivery;

public sealed class SendTransactionalSmsValidator : AbstractValidator<SendTransactionalSmsCommand>
{
    public SendTransactionalSmsValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Purpose).Must(p => p is Categories.Otp or Categories.PasswordReset).WithErrorCode("VAL.Purpose.Invalid");
        RuleFor(x => x.Text).NotEmpty().MaximumLength(500).WithErrorCode("VAL.Text.Invalid");
    }
}
