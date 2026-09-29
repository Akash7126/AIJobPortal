using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class ResetCredentialsValidator : AbstractValidator<ResetCredentialsCommand>
{
    public ResetCredentialsValidator() => RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
}
