using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Authentication;

namespace JobPlatform.AccountIdentity.Application.Validators.Authentication;

public sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithErrorCode("VAL.CurrentPassword.Required");
        RuleFor(x => x.NewPassword).NotEmpty().WithErrorCode("VAL.NewPassword.Required")
            .MaximumLength(128).WithErrorCode("VAL.NewPassword.MaxLength");
        RuleFor(x => x.NewPassword).Must((c, n) => n != c.CurrentPassword).WithErrorCode("VAL.NewPassword.SameAsCurrent");
    }
}
