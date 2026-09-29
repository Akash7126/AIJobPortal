using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Validators.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class BanUserAccountValidator : AbstractValidator<BanUserAccountCommand>
{
    public BanUserAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Reason).RequiredReason();
    }
}
