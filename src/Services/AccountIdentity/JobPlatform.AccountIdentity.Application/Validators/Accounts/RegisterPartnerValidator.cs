using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Accounts;
using JobPlatform.AccountIdentity.Application.Validators.Common;
using JobPlatform.AccountIdentity.Domain.Accounts;

namespace JobPlatform.AccountIdentity.Application.Validators.Accounts;

/// <summary>Malformed input only; password strength is a domain policy.</summary>
public sealed class RegisterPartnerValidator : AbstractValidator<RegisterPartnerAccountCommand>
{
    public RegisterPartnerValidator()
    {
        RuleFor(x => x.OrganisationName).NotEmpty().WithErrorCode("VAL.OrganisationName.Required")
            .MaximumLength(200).WithErrorCode("VAL.OrganisationName.TooLong");
        RuleFor(x => x.ContactEmail).RequiredEmail();
        RuleFor(x => x.Mobile).RequiredMobile();
        RuleFor(x => x.Identity).NotEmpty().WithErrorCode("VAL.Identity.Required")
            .MaximumLength(ExternalIdentityKey.MaxLength).WithErrorCode("VAL.Identity.TooLong");
        RuleFor(x => x.Password).RequiredPassword();
    }
}
