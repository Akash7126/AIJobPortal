using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;

namespace JobPlatform.AccountIdentity.Application.Validators.ApiCredentials;

public sealed class RevokeApiCredentialValidator : AbstractValidator<RevokeApiCredentialCommand>
{
    public RevokeApiCredentialValidator() => RuleFor(x => x.ApiCredentialId).NotEmpty().WithErrorCode("VAL.ApiCredentialId.Required");
}
