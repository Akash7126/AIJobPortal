using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Validators.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

/// <summary>Malformed input only; password strength is a domain policy.</summary>
public sealed class RegisterJobSeekerValidator : AbstractValidator<RegisterJobSeekerAccountCommand>
{
    public RegisterJobSeekerValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithErrorCode("VAL.FullName.Required")
            .MaximumLength(200).WithErrorCode("VAL.FullName.TooLong");
        RuleFor(x => x.Mobile).RequiredMobile();
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
            RuleFor(x => x.Email!).ValidEmail());
        RuleFor(x => x.Password).RequiredPassword();
        When(x => !string.IsNullOrWhiteSpace(x.PreferredLanguage), () =>
            RuleFor(x => x.PreferredLanguage!).Must(l => l is "ar" or "en").WithErrorCode("VAL.PreferredLanguage.Invalid"));
    }
}
