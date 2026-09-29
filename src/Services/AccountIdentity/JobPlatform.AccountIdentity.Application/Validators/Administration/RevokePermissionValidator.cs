using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Application.Validators.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Administration;

public sealed class RevokePermissionValidator : AbstractValidator<RevokePermissionCommand>
{
    public RevokePermissionValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithErrorCode("VAL.RoleId.Required");
        RuleFor(x => x.Permission).RequiredPermission();
    }
}
