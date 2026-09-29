using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class ActivateAccountValidator : AbstractValidator<ActivateAccountCommand>
{
    public ActivateAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Code).NotEmpty().WithErrorCode("VAL.Code.Required").DependentRules(() =>
            RuleFor(x => x.Code).Matches("^[0-9]{6}$").WithErrorCode("VAL.Code.Format"));
    }
}
