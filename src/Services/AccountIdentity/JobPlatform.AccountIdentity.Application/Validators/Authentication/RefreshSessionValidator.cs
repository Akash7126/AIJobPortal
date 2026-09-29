using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;

namespace JobPlatform.AccountIdentity.Application.Validators.Authentication;

public sealed class RefreshSessionValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionValidator() => RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("VAL.RefreshToken.Required");
}
