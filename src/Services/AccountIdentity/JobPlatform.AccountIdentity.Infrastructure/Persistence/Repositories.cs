using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class AccountRepository : IAccountRepository, IAccountUniquenessChecker
{
    private readonly IdentityDbContext _db;

    public AccountRepository(IdentityDbContext db) => _db = db;

    public Task<Account?> GetByIdAsync(AccountId id, CancellationToken ct = default) =>
        _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Account>> FindByLoginAsync(string login, ActorType? actorType, CancellationToken ct = default)
    {
        IQueryable<Account> query = _db.Accounts;
        if (login.Contains('@'))
        {
            if (!Email.TryCreate(login, out var email))
            {
                return Array.Empty<Account>();
            }

            query = query.Where(a => a.Email == email);
        }
        else
        {
            if (!MobileNumber.TryCreate(login, out var mobile))
            {
                return Array.Empty<Account>();
            }

            query = query.Where(a => a.Mobile == mobile);
        }

        if (actorType is { } type)
        {
            query = query.Where(a => a.ActorType == type);
        }

        return await query.OrderBy(a => a.CreatedAtUtc).ToListAsync(ct);
    }

    public void Add(Account account) => _db.Accounts.Add(account);

    public Task<bool> IsMobileTakenAsync(ActorType actorType, MobileNumber mobile, CancellationToken ct = default) =>
        _db.Accounts.AnyAsync(a => a.ActorType == actorType && a.Mobile == mobile, ct);

    public Task<bool> IsEmailTakenAsync(ActorType actorType, Email email, CancellationToken ct = default) =>
        _db.Accounts.AnyAsync(a => a.ActorType == actorType && a.Email == email, ct);

    public Task<bool> IsIdentityKeyTakenAsync(ActorType actorType, ExternalIdentityKey key, CancellationToken ct = default) =>
        _db.Accounts.AnyAsync(a => a.ActorType == actorType && a.IdentityKey == key, ct);
}

internal sealed class ApiCredentialRepository : IApiCredentialRepository
{
    private readonly IdentityDbContext _db;

    public ApiCredentialRepository(IdentityDbContext db) => _db = db;

    public Task<ApiCredential?> GetByIdAsync(ApiCredentialId id, CancellationToken ct = default) =>
        _db.ApiCredentials.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<ApiCredential?> GetByKeyIdAsync(string keyId, CancellationToken ct = default) =>
        _db.ApiCredentials.FirstOrDefaultAsync(c => c.KeyId == keyId, ct);

    public Task<ApiCredential?> GetActiveByPartnerAsync(AccountId partnerAccountId, CancellationToken ct = default) =>
        _db.ApiCredentials.FirstOrDefaultAsync(c => c.PartnerAccountId == partnerAccountId && c.Status == ApiCredentialStatus.Active, ct);

    public void Add(ApiCredential credential) => _db.ApiCredentials.Add(credential);
}

internal sealed class PasswordPolicyRepository : IPasswordPolicyRepository
{
    private readonly IdentityDbContext _db;
    private readonly TimeProvider _clock;

    public PasswordPolicyRepository(IdentityDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PasswordPolicy> GetAsync(CancellationToken ct = default)
    {
        var policy = await _db.PasswordPolicies.FirstOrDefaultAsync(p => p.Id == PasswordPolicy.SingletonId, ct);
        if (policy is not null)
        {
            return policy;
        }

        policy = PasswordPolicy.CreateDefault(_clock);
        _db.PasswordPolicies.Add(policy);
        return policy;
    }
}

internal sealed class SessionTimeoutSettingRepository : ISessionTimeoutSettingRepository
{
    private readonly IdentityDbContext _db;
    private readonly TimeProvider _clock;

    public SessionTimeoutSettingRepository(IdentityDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<SessionTimeoutSetting> GetAsync(CancellationToken ct = default)
    {
        var setting = await _db.SessionSettings.FirstOrDefaultAsync(s => s.Id == SessionTimeoutSetting.SingletonId, ct);
        if (setting is not null)
        {
            return setting;
        }

        setting = SessionTimeoutSetting.CreateDefault(_clock);
        _db.SessionSettings.Add(setting);
        return setting;
    }
}

internal sealed class RoleRepository : IRoleRepository
{
    private readonly IdentityDbContext _db;

    public RoleRepository(IdentityDbContext db) => _db = db;

    public Task<Role?> GetByIdAsync(RoleId id, CancellationToken ct = default) => _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken ct = default) => await _db.Roles.OrderBy(r => r.Name).ToListAsync(ct);

    public void Add(Role role) => _db.Roles.Add(role);
}

internal sealed class PrivacyConsentRepository : IPrivacyConsentRepository
{
    private readonly IdentityDbContext _db;

    public PrivacyConsentRepository(IdentityDbContext db) => _db = db;

    public Task<PrivacyConsent?> GetAsync(Guid guestId, string policyVersion, CancellationToken ct = default) =>
        _db.PrivacyConsents.FirstOrDefaultAsync(c => c.GuestId == guestId && c.PolicyVersion == policyVersion, ct);

    public void Add(PrivacyConsent consent) => _db.PrivacyConsents.Add(consent);
}
