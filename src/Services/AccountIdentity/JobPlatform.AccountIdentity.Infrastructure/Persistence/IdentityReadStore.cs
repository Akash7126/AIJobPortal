using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Application.DTOs.Accounts;
using JobPlatform.AccountIdentity.Application.DTOs.Administration;
using JobPlatform.AccountIdentity.Application.DTOs.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Interfaces;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.SharedKernel.ApiContracts.AccountIdentity;
using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence;

/// <summary>Read side (foundation section 5): AsNoTracking projections, never aggregates.</summary>
internal sealed class IdentityReadStore : IIdentityReadStore
{
    private static readonly TimeSpan PolicyCacheTtl = TimeSpan.FromMinutes(10);

    private readonly IdentityDbContext _db;
    private readonly ICacheStore _cache;
    private readonly ILogger<IdentityReadStore> _logger;

    public IdentityReadStore(IdentityDbContext db, ICacheStore cache, ILogger<IdentityReadStore> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AccountSummaryDto?> GetAccountSummaryAsync(Guid accountId, CancellationToken ct = default)
    {
        var id = new AccountId(accountId);
        var row = await _db.Accounts.AsNoTracking().Where(a => a.Id == id)
            .Select(a => new { a.ActorType, a.Standing, a.CreatedAtUtc, a.ActivatedAtUtc }).FirstOrDefaultAsync(ct);
        return row is null ? null : new AccountSummaryDto(accountId, row.ActorType, row.Standing.ToString(), row.CreatedAtUtc, row.ActivatedAtUtc);
    }

    public async Task<AccountStandingView?> GetAccountStandingAsync(Guid accountId, CancellationToken ct = default)
    {
        var id = new AccountId(accountId);
        var now = DateTime.UtcNow;
        var row = await _db.Accounts.AsNoTracking().Where(a => a.Id == id).Select(a => new
        {
            a.ActorType, a.DisplayName, a.Email, a.Mobile, a.Standing, a.LockedUntilUtc, a.MfaEnabled, a.EmailVerifiedAtUtc, a.MustChangePassword,
            a.CreatedAtUtc, a.ActivatedAtUtc, a.RowVersion,
            History = a.StatusHistory.OrderBy(h => h.AtUtc).Select(h => new { h.From, h.To, h.By, h.Reason, h.AtUtc }).ToList()
        }).FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new AccountStandingView(accountId, row.ActorType, row.DisplayName, row.Email?.Value, row.Mobile.Value, row.Standing.ToString(),
                row.LockedUntilUtc > now, row.LockedUntilUtc, row.MfaEnabled, row.EmailVerifiedAtUtc is not null, row.MustChangePassword,
                row.CreatedAtUtc, row.ActivatedAtUtc,
                row.History.Select(h => new AccountStatusChangeView(h.From?.ToString(), h.To.ToString(), h.By, h.Reason, h.AtUtc)).ToList(),
                Array.Empty<string>(), ETag.From(row.RowVersion));
    }

    public async Task<PagedResult<AccountListItemView>> ListAccountsAsync(AccountListFilter filter, PageRequest page, CancellationToken ct = default)
    {
        IQueryable<Account> query = _db.Accounts.AsNoTracking();
        if (filter.ActorType is { } actorType)
        {
            query = query.Where(a => a.ActorType == actorType);
        }

        if (filter.Standing is { Length: > 0 } standingText && Enum.TryParse<AccountStanding>(standingText, out var standing))
        {
            query = query.Where(a => a.Standing == standing);
        }

        if (filter.Search is { Length: > 0 } search)
        {
            if (MobileNumber.TryCreate(search, out var mobile))
            {
                query = query.Where(a => a.DisplayName.Contains(search) || a.Mobile == mobile);
            }
            else if (Email.TryCreate(search, out var email))
            {
                query = query.Where(a => a.DisplayName.Contains(search) || a.Email == email);
            }
            else
            {
                query = query.Where(a => a.DisplayName.Contains(search));
            }
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.CreatedAtUtc).Skip(page.Skip).Take(page.PageSize)
            .Select(a => new { a.Id, a.ActorType, a.DisplayName, a.Email, a.Mobile, a.Standing, a.CreatedAtUtc }).ToListAsync(ct);

        var items = rows.Select(r => new AccountListItemView(r.Id.Value, r.ActorType, r.DisplayName, r.Email?.Value, r.Mobile.Value,
            r.Standing.ToString(), r.CreatedAtUtc)).ToList();
        return new PagedResult<AccountListItemView>(items, page.Page, page.PageSize, total);
    }

    public async Task<ApiCredentialControlsDto?> GetApiCredentialControlsAsync(Guid apiCredentialId, CancellationToken ct = default)
    {
        var id = new ApiCredentialId(apiCredentialId);
        var row = await _db.ApiCredentials.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new { c.PartnerAccountId, c.KeyId, c.Status, c.IpWhitelist, c.Limits, c.ExpiresAtUtc }).FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new ApiCredentialControlsDto(apiCredentialId, row.PartnerAccountId.Value, row.KeyId, row.Status.ToString(), row.IpWhitelist.Entries,
                row.Limits.MaxRequests, row.Limits.PeriodSeconds, row.ExpiresAtUtc);
    }

    public async Task<ApiCredentialView?> GetActiveApiCredentialForPartnerAsync(Guid partnerAccountId, CancellationToken ct = default)
    {
        var partner = new AccountId(partnerAccountId);
        var row = await _db.ApiCredentials.AsNoTracking().Where(c => c.PartnerAccountId == partner && c.Status == ApiCredentialStatus.Active)
            .Select(c => new { c.Id, c.KeyId, c.Status, c.IpWhitelist, c.Limits, c.IssuedAtUtc, c.ExpiresAtUtc }).FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new ApiCredentialView(row.Id.Value, row.KeyId, row.Status.ToString(), row.IpWhitelist.Entries, row.Limits.MaxRequests,
                row.Limits.PeriodSeconds, row.IssuedAtUtc, row.ExpiresAtUtc);
    }

    public async Task<PagedResult<AccessLogEntryDto>> ListAccessLogAsync(DateTime? fromUtc, DateTime? toUtc, Guid? accountId, PageRequest page,
        CancellationToken ct = default)
    {
        IQueryable<AccessLogRecord> query = _db.AccessLog.AsNoTracking();
        if (fromUtc is { } from)
        {
            query = query.Where(l => l.AtUtc >= from);
        }

        if (toUtc is { } to)
        {
            query = query.Where(l => l.AtUtc <= to);
        }

        if (accountId is { } account)
        {
            query = query.Where(l => l.AccountId == account);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.AtUtc).Skip(page.Skip).Take(page.PageSize)
            .Select(l => new AccessLogEntryDto(l.Id, l.AtUtc, l.AccountId, l.Action, l.Resource, l.Decision, l.Reason, l.IpAddress)).ToListAsync(ct);
        return new PagedResult<AccessLogEntryDto>(items, page.Page, page.PageSize, total);
    }

    /// <summary>Cache-aside (Redis key passwordpolicy, 10 min); a cache failure degrades to the database.</summary>
    public async Task<PasswordPolicyView> GetPasswordPolicyAsync(CancellationToken ct = default)
    {
        try
        {
            if (await _cache.GetJsonAsync<PasswordPolicyView>(CacheKeys.PasswordPolicy, ct) is { } cached)
            {
                return cached;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password policy cache read failed; reading from the database");
        }

        var row = await _db.PasswordPolicies.AsNoTracking()
            .Where(p => p.Id == PasswordPolicy.SingletonId)
            .Select(p => new { p.MinLength, p.RequireUpper, p.RequireLower, p.RequireDigit, p.PolicyVersion, p.UpdatedAtUtc, p.RowVersion })
            .FirstOrDefaultAsync(ct);
        var view = row is null
            ? new PasswordPolicyView(AccountDefaults.DefaultPasswordMinLength, true, true, true, 1, DateTime.UtcNow)
            : new PasswordPolicyView(row.MinLength, row.RequireUpper, row.RequireLower, row.RequireDigit, row.PolicyVersion, row.UpdatedAtUtc, ETag.From(row.RowVersion));

        try
        {
            await _cache.SetJsonAsync(CacheKeys.PasswordPolicy, view, PolicyCacheTtl, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password policy cache write failed");
        }

        return view;
    }

    public async Task<SessionTimeoutView> GetSessionTimeoutAsync(CancellationToken ct = default)
    {
        var row = await _db.SessionSettings.AsNoTracking().Where(s => s.Id == SessionTimeoutSetting.SingletonId)
            .Select(s => new { s.IdleTimeoutMinutes, s.SettingVersion, s.UpdatedAtUtc, s.RowVersion }).FirstOrDefaultAsync(ct);
        return row is null
            ? new SessionTimeoutView(AccountDefaults.DefaultIdleTimeoutMinutes, 1, DateTime.UtcNow)
            : new SessionTimeoutView(row.IdleTimeoutMinutes, row.SettingVersion, row.UpdatedAtUtc, ETag.From(row.RowVersion));
    }

    public async Task<IReadOnlyList<RoleView>> ListRolesAsync(CancellationToken ct = default)
    {
        var rows = await _db.Roles.AsNoTracking().OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.IsSystem, r.RowVersion, Permissions = r.Permissions.Select(p => p.Permission).OrderBy(p => p).ToList() })
            .ToListAsync(ct);
        return rows.Select(r => new RoleView(r.Id.Value, r.Name, r.IsSystem, r.Permissions, ETag.From(r.RowVersion))).ToList();
    }

    public async Task<ConsentDecisionView?> GetConsentAsync(Guid guestId, string policyVersion, CancellationToken ct = default)
    {
        var row = await _db.PrivacyConsents.AsNoTracking().Where(c => c.GuestId == guestId && c.PolicyVersion == policyVersion)
            .Select(c => new { c.GuestId, c.PolicyVersion, c.Choices, c.DecidedAtUtc, c.Locale }).FirstOrDefaultAsync(ct);
        return row is null
            ? null
            : new ConsentDecisionView(row.GuestId, row.PolicyVersion, row.Choices.Analytics, row.Choices.Preferences, row.Choices.Marketing,
                row.DecidedAtUtc, row.Locale.ToString());
    }
}

/// <summary>Redis key names (handover section 9). The environment/BC prefix is added by the cache store.</summary>
internal static class CacheKeys
{
    public const string PasswordPolicy = "passwordpolicy";
    public const string RoleMap = "rbac:roles";

    public static string AccountRoles(Guid accountId) => $"rbac:account-roles:{accountId}";

    public static string Session(Guid sessionId) => $"session:{sessionId}";

    public static string AccountSessions(Guid accountId) => $"session:account:{accountId}";

    public static string RevokedToken(string jti) => $"revoked:{jti}";

    public static string RevokedClient(string clientId) => $"revoked-client:{clientId}";

    public static string MfaChallenge(string tokenHash) => $"mfa-challenge:{tokenHash}";

    public static string LoginCode(Guid accountId) => $"login-code:{accountId}";
}
