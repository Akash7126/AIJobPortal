using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;

namespace JobPlatform.AccountIdentity.Application.Validators.ApiCredentials;

public sealed class AuthenticateApiClientValidator : AbstractValidator<AuthenticateApiClientCommand>
{
    public AuthenticateApiClientValidator()
    {
        RuleFor(x => x.GrantType).Equal("client_credentials").WithErrorCode("VAL.GrantType.Unsupported");
        RuleFor(x => x.ClientId).NotEmpty().WithErrorCode("VAL.ClientId.Required").MaximumLength(128).WithErrorCode("VAL.ClientId.TooLong");
        RuleFor(x => x.ClientSecret).NotEmpty().WithErrorCode("VAL.ClientSecret.Required").MaximumLength(256).WithErrorCode("VAL.ClientSecret.TooLong");
    }
}
