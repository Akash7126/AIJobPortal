using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Validators.Common;
using JobPlatform.AccountIdentity.Domain.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

/// <summary>Malformed input only; password strength is a domain policy.</summary>
public sealed class RegisterEmployerValidator : AbstractValidator<RegisterEmployerAccountCommand>
{
    public RegisterEmployerValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithErrorCode("VAL.CompanyName.Required")
            .MaximumLength(200).WithErrorCode("VAL.CompanyName.TooLong");
        RuleFor(x => x.Email).RequiredEmail();
        RuleFor(x => x.Mobile).RequiredMobile();
        RuleFor(x => x.CompanyId).NotEmpty().WithErrorCode("VAL.CompanyId.Required")
            .MaximumLength(ExternalIdentityKey.MaxLength).WithErrorCode("VAL.CompanyId.TooLong");
        RuleFor(x => x.RegistrationNumber).NotEmpty().WithErrorCode("VAL.RegistrationNumber.Required")
            .MaximumLength(64).WithErrorCode("VAL.RegistrationNumber.TooLong");
        RuleFor(x => x.Level).Must(l => l is 1 or 2).WithErrorCode("VAL.Level.Invalid");
        RuleFor(x => x.Password).RequiredPassword();
    }
}
