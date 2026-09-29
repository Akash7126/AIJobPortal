using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class AssignRoleToAccountValidator : AbstractValidator<AssignRoleToAccountCommand>
{
    public AssignRoleToAccountValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.RoleId).NotEmpty().WithErrorCode("VAL.RoleId.Required");
    }
}
