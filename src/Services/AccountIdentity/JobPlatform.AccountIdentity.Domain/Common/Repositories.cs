using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Interfaces.Services;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.Common;

/// <summary>Domain service: enforces INV-01 before creating an account.</summary>
public sealed class AccountRegistrar
{
    private readonly IAccountUniquenessChecker _uniqueness;
    private readonly TimeProvider _clock;

    public AccountRegistrar(IAccountUniquenessChecker uniqueness, TimeProvider clock)
    {
        _uniqueness = uniqueness;
        _clock = clock;
    }

    public async Task<Account> RegisterAsync(RegistrationDetails details, PasswordHash passwordHash, CancellationToken ct = default)
    {
        var duplicate =
            await _uniqueness.IsMobileTakenAsync(details.ActorType, details.Mobile, ct)
            || (details.Email is not null && await _uniqueness.IsEmailTakenAsync(details.ActorType, details.Email, ct))
            || (details.IdentityKey is not null && await _uniqueness.IsIdentityKeyTakenAsync(details.ActorType, details.IdentityKey, ct));

        if (duplicate)
        {
            throw new BusinessRuleViolationException(AccountRuleCodes.Duplicate, "An account with these details already exists.",
                ErrorCodes.Duplicate(details.ActorType), BusinessRuleKind.Conflict);
        }

        return Account.Register(details, passwordHash, _clock);
    }
}
