using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class RemoveRoleFromAccountValidator : AbstractValidator<RemoveRoleFromAccountCommand>
{
    public RemoveRoleFromAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.RoleId).NotEmpty().WithErrorCode("VAL.RoleId.Required");
    }
}
