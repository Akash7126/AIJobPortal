using FluentValidation;
using JobPlatform.AccountIdentity.Application.Queries.Internal;
using JobPlatform.AccountIdentity.Application.Validators.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Internal;

public sealed class CheckPermissionValidator : AbstractValidator<CheckPermissionQuery>
{
    public CheckPermissionValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Permission).RequiredPermission();
    }
}
