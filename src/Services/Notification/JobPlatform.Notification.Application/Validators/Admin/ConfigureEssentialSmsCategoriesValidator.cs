using FluentValidation;
using JobPlatform.Notification.Application.Commands.Admin;
using JobPlatform.Notification.Domain;

namespace JobPlatform.Notification.Application.Validators.Admin;

public sealed class ConfigureEssentialSmsCategoriesValidator : AbstractValidator<ConfigureEssentialSmsCategoriesCommand>
{
    public ConfigureEssentialSmsCategoriesValidator()
    {
        RuleFor(x => x.Categories).NotNull().WithErrorCode("VAL.Categories.Required");
        RuleForEach(x => x.Categories).Must(Categories.IsKnown).WithErrorCode("VAL.Category.Unknown");
        RuleFor(x => x.Categories).Must(c => Categories.DefaultEssentialSms.All(e => c.Contains(e, StringComparer.OrdinalIgnoreCase)))
            .WithErrorCode("VAL.Categories.OtpAndResetRequired");
    }
}
