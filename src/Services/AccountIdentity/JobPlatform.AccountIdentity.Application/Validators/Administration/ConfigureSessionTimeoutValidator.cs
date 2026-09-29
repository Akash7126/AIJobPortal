using FluentValidation;
using JobPlatform.AccountIdentity.Application.Commands.Administration;
using JobPlatform.AccountIdentity.Domain.Common;

namespace JobPlatform.AccountIdentity.Application.Validators.Administration;

public sealed class ConfigureSessionTimeoutValidator : AbstractValidator<ConfigureSessionTimeoutCommand>
{
    public ConfigureSessionTimeoutValidator() =>
        RuleFor(x => x.IdleTimeoutMinutes).InclusiveBetween(AccountDefaults.MinIdleTimeoutMinutes, AccountDefaults.MaxIdleTimeoutMinutes)
            .WithErrorCode("VAL.IdleTimeoutMinutes.Range");
}
