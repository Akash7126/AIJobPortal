using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.Common;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(AccountId id, CancellationToken ct = default);

    /// <summary>Finds candidates by e-mail or mobile. One person may hold accounts of several actor types with the same mobile.</summary>
    Task<IReadOnlyList<Account>> FindByLoginAsync(string login, ActorType? actorType, CancellationToken ct = default);

    void Add(Account account);
}

public interface IApiCredentialRepository
{
    Task<ApiCredential?> GetByIdAsync(ApiCredentialId id, CancellationToken ct = default);

    Task<ApiCredential?> GetByKeyIdAsync(string keyId, CancellationToken ct = default);

    /// <summary>The credential in status Active for the partner (even when past its expiry, until revoked).</summary>
    Task<ApiCredential?> GetActiveByPartnerAsync(AccountId partnerAccountId, CancellationToken ct = default);

    void Add(ApiCredential credential);
}

public interface IPasswordPolicyRepository
{
    /// <summary>Returns the singleton policy, creating the default when it does not exist yet.</summary>
    Task<PasswordPolicy> GetAsync(CancellationToken ct = default);
}

public interface ISessionTimeoutSettingRepository
{
    Task<SessionTimeoutSetting> GetAsync(CancellationToken ct = default);
}

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(RoleId id, CancellationToken ct = default);

    Task<IReadOnlyList<Role>> ListAsync(CancellationToken ct = default);

    void Add(Role role);
}

public interface IPrivacyConsentRepository
{
    Task<PrivacyConsent?> GetAsync(Guid guestId, string policyVersion, CancellationToken ct = default);

    void Add(PrivacyConsent consent);
}

/// <summary>Duplicate detection across mobile/e-mail/company id/identity (INV-01). Unique indexes remain the final guard.</summary>
public interface IAccountUniquenessChecker
{
    Task<bool> IsMobileTakenAsync(ActorType actorType, MobileNumber mobile, CancellationToken ct = default);

    Task<bool> IsEmailTakenAsync(ActorType actorType, Email email, CancellationToken ct = default);

    Task<bool> IsIdentityKeyTakenAsync(ActorType actorType, ExternalIdentityKey key, CancellationToken ct = default);
}

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
