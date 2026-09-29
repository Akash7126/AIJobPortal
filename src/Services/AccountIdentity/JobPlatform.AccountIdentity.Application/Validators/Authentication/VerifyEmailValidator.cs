using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;

namespace JobPlatform.AccountIdentity.Application.Validators.Authentication;

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty().WithErrorCode("VAL.AccountId.Required");
        RuleFor(x => x.Token).NotEmpty().WithErrorCode("VAL.Token.Required").MaximumLength(256).WithErrorCode("VAL.Token.TooLong");
    }
}
