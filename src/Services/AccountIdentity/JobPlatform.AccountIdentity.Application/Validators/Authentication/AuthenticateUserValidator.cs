using FluentValidation;
using JobPlatform.AccountIdentity.Application.Authentication;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;
using JobPlatform.AccountIdentity.Application.Validators.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Authentication;

public sealed class AuthenticateUserValidator : AbstractValidator<AuthenticateUserCommand>
{
    private static readonly string[] Mechanisms = { LoginMechanisms.Password, LoginMechanisms.EmailVerification, LoginMechanisms.Mfa };

    public AuthenticateUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithErrorCode("VAL.Username.Required").MaximumLength(254).WithErrorCode("VAL.Username.TooLong");
        RuleFor(x => x.Mechanism).Must(m => m is not null && Mechanisms.Contains(m)).WithErrorCode("VAL.Mechanism.Invalid");
        When(x => x.Mechanism is LoginMechanisms.Password or LoginMechanisms.Mfa, () =>
            RuleFor(x => x.Password).RequiredPassword());
        When(x => x.Mechanism == LoginMechanisms.Mfa, () =>
            RuleFor(x => x.MfaCode).NotEmpty().WithErrorCode("VAL.MfaCode.Required").DependentRules(() =>
                RuleFor(x => x.MfaCode!).Matches("^[0-9]{6}$").WithErrorCode("VAL.MfaCode.Format")));
        When(x => x.Mechanism == LoginMechanisms.EmailVerification, () =>
        {
            RuleFor(x => x.Username).ValidEmail();
            When(x => !string.IsNullOrEmpty(x.EmailCode), () =>
                RuleFor(x => x.EmailCode!).Matches("^[0-9]{6}$").WithErrorCode("VAL.EmailCode.Format"));
        });
    }
}
