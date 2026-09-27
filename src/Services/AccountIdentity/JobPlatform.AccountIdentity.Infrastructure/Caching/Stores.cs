using System.Security.Cryptography;
using System.Text;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AccountIdentity.Infrastructure.Caching;

/// <summary>
/// Sessions in Redis (or the in-memory fallback): key session:{id} with a sliding TTL equal to the idle timeout captured at creation.
/// Auth-critical, so cache failures propagate (fail closed) instead of silently allowing access.
/// </summary>
public sealed class CacheSessionStore : ISessionStore
{
    private static readonly TimeSpan IndexTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan InvalidatedRetention = TimeSpan.FromMinutes(5);

    private readonly ICacheStore _cache;
    private readonly TimeProvider _clock;

    public CacheSessionStore(ICacheStore cache, TimeProvider clock)
    {
        _cache = cache;
        _clock = clock;
    }

    public async Task CreateAsync(UserSession session, CancellationToken ct = default)
    {
        await _cache.SetJsonAsync(CacheKeys.Session(session.SessionId), session.ToSnapshot(), session.IdleTimeout, ct);
        await _cache.SetAddAsync(CacheKeys.AccountSessions(session.AccountId), session.SessionId.ToString(), IndexTtl, ct);
    }

    public async Task<UserSession?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        var snapshot = await _cache.GetJsonAsync<SessionSnapshot>(CacheKeys.Session(sessionId), ct);
        return snapshot is null ? null : UserSession.Rehydrate(snapshot);
    }

    public async Task SaveAsync(UserSession session, CancellationToken ct = default)
    {
        var ttl = session.Status == SessionStatus.Active ? session.IdleTimeout : InvalidatedRetention;
        await _cache.SetJsonAsync(CacheKeys.Session(session.SessionId), session.ToSnapshot(), ttl, ct);
        if (session.Status == SessionStatus.Active)
        {
            await _cache.SetAddAsync(CacheKeys.AccountSessions(session.AccountId), session.SessionId.ToString(), IndexTtl, ct);
        }
    }

    public async Task InvalidateAllForAccountAsync(Guid accountId, Guid? exceptSessionId = null, CancellationToken ct = default)
    {
        foreach (var member in await _cache.SetMembersAsync(CacheKeys.AccountSessions(accountId), ct))
        {
            if (!Guid.TryParse(member, out var sessionId) || sessionId == exceptSessionId)
            {
                continue;
            }

            if (await GetAsync(sessionId, ct) is { } session)
            {
                session.Invalidate();
                await SaveAsync(session, ct);
            }

            await _cache.SetRemoveAsync(CacheKeys.AccountSessions(accountId), member, ct);
        }
    }

    public Task RevokeTokenAsync(string tokenId, TimeSpan ttl, CancellationToken ct = default) =>
        _cache.SetAsync(CacheKeys.RevokedToken(tokenId), "1", ttl, ct);

    public async Task<bool> IsTokenRevokedAsync(string tokenId, CancellationToken ct = default) =>
        await _cache.GetAsync(CacheKeys.RevokedToken(tokenId), ct) is not null;

    public Task RevokeClientAsync(string clientId, TimeSpan ttl, CancellationToken ct = default) =>
        _cache.SetAsync(CacheKeys.RevokedClient(clientId), "1", ttl, ct);

    public async Task<bool> IsClientRevokedAsync(string clientId, CancellationToken ct = default) =>
        await _cache.GetAsync(CacheKeys.RevokedClient(clientId), ct) is not null;
}

public sealed class CacheMfaChallengeStore : IMfaChallengeStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private readonly ICacheStore _cache;
    private readonly ISecretGenerator _generator;
    private readonly TimeProvider _clock;

    public CacheMfaChallengeStore(ICacheStore cache, ISecretGenerator generator, TimeProvider clock)
    {
        _cache = cache;
        _generator = generator;
        _clock = clock;
    }

    public async Task<MfaChallengeToken> CreateAsync(Guid accountId, CancellationToken ct = default)
    {
        var token = _generator.GenerateToken();
        await _cache.SetAsync(CacheKeys.MfaChallenge(Hash(token)), accountId.ToString(), Ttl, ct);
        return new MfaChallengeToken(token, _clock.GetUtcNow().UtcDateTime + Ttl);
    }

    public async Task<Guid?> GetAccountIdAsync(string token, CancellationToken ct = default) =>
        Guid.TryParse(await _cache.GetAsync(CacheKeys.MfaChallenge(Hash(token)), ct), out var id) ? id : null;

    public Task DeleteAsync(string token, CancellationToken ct = default) => _cache.RemoveAsync(CacheKeys.MfaChallenge(Hash(token)), ct);

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class CacheLoginCodeStore : ILoginCodeStore
{
    private readonly ICacheStore _cache;

    public CacheLoginCodeStore(ICacheStore cache) => _cache = cache;

    public Task StoreAsync(Guid accountId, string codeHash, TimeSpan ttl, CancellationToken ct = default) =>
        _cache.SetJsonAsync(CacheKeys.LoginCode(accountId), new Entry(codeHash, 0), ttl, ct);

    public async Task<bool> VerifyAndConsumeAsync(Guid accountId, string code, ISecretVerifier verifier, int maxAttempts, CancellationToken ct = default)
    {
        var key = CacheKeys.LoginCode(accountId);
        var entry = await _cache.GetJsonAsync<Entry>(key, ct);
        if (entry is null)
        {
            return false;
        }

        if (entry.Attempts >= maxAttempts)
        {
            await _cache.RemoveAsync(key, ct);
            return false;
        }

        if (verifier.Verify(code, entry.Hash))
        {
            await _cache.RemoveAsync(key, ct);
            return true;
        }

        var remaining = await _cache.GetTimeToLiveAsync(key, ct) ?? TimeSpan.FromMinutes(1);
        await _cache.SetJsonAsync(key, entry with { Attempts = entry.Attempts + 1 }, remaining, ct);
        return false;
    }

    private sealed record Entry(string Hash, int Attempts);
}

/// <summary>Role-to-permission map (Redis key rbac:roles) and per-account role ids (rbac:account-roles:{id}), cache-aside with DB fallback.</summary>
public sealed class CachedRoleDirectory : IRoleDirectory
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IdentityDbContext _db;
    private readonly ICacheStore _cache;
    private readonly ILogger<CachedRoleDirectory> _logger;

    public CachedRoleDirectory(IdentityDbContext db, ICacheStore cache, ILogger<CachedRoleDirectory> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RolePermissions>> GetRolesForAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var roleIds = await GetRoleIdsAsync(accountId, ct);
        var map = await GetRoleMapAsync(ct);
        return roleIds.Where(map.ContainsKey).Select(id => map[id]).ToList();
    }

    private async Task<IReadOnlyList<Guid>> GetRoleIdsAsync(Guid accountId, CancellationToken ct)
    {
        var key = CacheKeys.AccountRoles(accountId);
        var cached = await TryGetAsync<Guid[]>(key, ct);
        if (cached is not null)
        {
            return cached;
        }

        var id = new AccountId(accountId);
        var rows = await _db.Accounts.AsNoTracking().Where(a => a.Id == id).SelectMany(a => a.RoleAssignments).Select(r => r.RoleId).ToListAsync(ct);
        var ids = rows.Select(r => r.Value).ToArray();
        await TrySetAsync(key, ids, ct);
        return ids;
    }

    private async Task<Dictionary<Guid, RolePermissions>> GetRoleMapAsync(CancellationToken ct)
    {
        var cached = await TryGetAsync<List<RoleEntry>>(CacheKeys.RoleMap, ct);
        if (cached is null)
        {
            var rows = await _db.Roles.AsNoTracking()
                .Select(r => new { r.Id, r.Name, Permissions = r.Permissions.Select(p => p.Permission).ToList() }).ToListAsync(ct);
            cached = rows.Select(r => new RoleEntry(r.Id.Value, r.Name, r.Permissions)).ToList();
            await TrySetAsync(CacheKeys.RoleMap, cached, ct);
        }

        return cached.ToDictionary(r => r.Id,
            r => new RolePermissions(new RoleId(r.Id), r.Name, r.Permissions.ToHashSet(StringComparer.Ordinal)));
    }

    private async Task<T?> TryGetAsync<T>(string key, CancellationToken ct) where T : class
    {
        try
        {
            return await _cache.GetJsonAsync<T>(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Role cache read failed for {Key}; using the database", key);
            return null;
        }
    }

    private async Task TrySetAsync<T>(string key, T value, CancellationToken ct)
    {
        try
        {
            await _cache.SetJsonAsync(key, value, Ttl, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Role cache write failed for {Key}", key);
        }
    }

    private sealed record RoleEntry(Guid Id, string Name, List<string> Permissions);
}

public sealed class CacheInvalidator : ICacheInvalidator
{
    private readonly ICacheStore _cache;

    public CacheInvalidator(ICacheStore cache) => _cache = cache;

    public async Task InvalidateRoleMapAsync(CancellationToken ct = default) => await _cache.RemoveAsync(CacheKeys.RoleMap, ct);

    public async Task InvalidateAccountRolesAsync(Guid accountId, CancellationToken ct = default) =>
        await _cache.RemoveAsync(CacheKeys.AccountRoles(accountId), ct);

    public async Task InvalidatePasswordPolicyAsync(CancellationToken ct = default) => await _cache.RemoveAsync(CacheKeys.PasswordPolicy, ct);
}
