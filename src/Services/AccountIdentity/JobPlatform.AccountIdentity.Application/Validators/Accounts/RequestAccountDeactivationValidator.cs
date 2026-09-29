using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Validators.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class RequestAccountDeactivationValidator : AbstractValidator<RequestAccountDeactivationCommand>
{
    public RequestAccountDeactivationValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Kind).Must(k => k is "Deactivate" or "Delete").WithErrorCode("VAL.Kind.Invalid");
        RuleFor(x => x.Reason).RequiredReason();
    }
}
