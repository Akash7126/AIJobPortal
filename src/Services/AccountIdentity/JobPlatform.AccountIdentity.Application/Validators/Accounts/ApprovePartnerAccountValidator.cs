using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class ApprovePartnerAccountValidator : AbstractValidator<ApprovePartnerAccountCommand>
{
    public ApprovePartnerAccountValidator() =>
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
}
