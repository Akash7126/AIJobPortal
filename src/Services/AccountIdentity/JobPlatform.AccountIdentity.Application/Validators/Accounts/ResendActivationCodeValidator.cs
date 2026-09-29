using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

public sealed class ResendActivationCodeValidator : AbstractValidator<ResendActivationCodeCommand>
{
    public ResendActivationCodeValidator() =>
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
}
