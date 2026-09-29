using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;

namespace JobPlatform.AccountIdentity.Application.Validators.Authentication;

public sealed class BeginMfaEnrollmentValidator : AbstractValidator<BeginMfaEnrollmentCommand>
{
    public BeginMfaEnrollmentValidator() => RuleFor(x => x.MfaToken).NotEmpty().WithErrorCode("VAL.MfaToken.Required");
}
