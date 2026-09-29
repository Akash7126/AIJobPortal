using FluentValidation;
using JobPlatform.Notification.Application.Commands.Admin;

namespace JobPlatform.Notification.Application.Validators.Admin;

public sealed class EditEmailTemplateValidator : AbstractValidator<EditEmailTemplateCommand>
{
    public EditEmailTemplateValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64).WithErrorCode("VAL.Code.Required");
        RuleFor(x => x.Locale).Must(l => l is "ar" or "en").WithErrorCode("VAL.Locale.Invalid");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Subject.Invalid");
        RuleFor(x => x.Body).NotEmpty().MaximumLength(100_000).WithErrorCode("VAL.Body.Invalid");
        RuleFor(x => x.Placeholders).NotNull().WithErrorCode("VAL.Placeholders.Required");
    }
}
