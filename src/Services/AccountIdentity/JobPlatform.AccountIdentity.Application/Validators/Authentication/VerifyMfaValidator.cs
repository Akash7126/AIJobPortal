using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;

namespace JobPlatform.AccountIdentity.Application.Validators.Authentication;

public sealed class VerifyMfaValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaValidator()
    {
        RuleFor(x => x.MfaToken).NotEmpty().WithErrorCode("VAL.MfaToken.Required");
        RuleFor(x => x.Code).NotEmpty().WithErrorCode("VAL.MfaCode.Required").DependentRules(() =>
            RuleFor(x => x.Code).Matches("^[0-9]{6}$").WithErrorCode("VAL.MfaCode.Format"));
    }
}
