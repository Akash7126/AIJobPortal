using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class ApproveUserAccountValidator : AbstractValidator<ApproveUserAccountCommand>
{
    public ApproveUserAccountValidator() => RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
}
