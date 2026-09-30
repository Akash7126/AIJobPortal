using System.Security.Cryptography;
using System.Text;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Persistence;
using JobPlatform.SharedKernel.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.BuildingBlocks.Infrastructure.Persistence;

/// <summary>messaging.IdempotencyKeys row (foundation section 8): the durable record behind the Idempotency-Key header.</summary>
public sealed class IdempotencyKeyRecord
{
    public string Scope { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public bool IsSuccess { get; set; }

    /// <summary>The successful response, replayed for a repeated key. Never holds secrets: commands that return one are not idempotent.</summary>
    public string? PayloadJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}

internal sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKeyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyKeyRecord> builder)
    {
        builder.ToTable("IdempotencyKeys", BaseDbContext.MessagingSchema);
        builder.HasKey(k => new { k.Scope, k.Key });
        builder.Property(k => k.Scope).HasMaxLength(200);
        builder.Property(k => k.Key).HasMaxLength(128);
        builder.Property(k => k.Fingerprint).HasMaxLength(64).IsRequired();
        builder.HasIndex(k => k.ExpiresAtUtc).HasDatabaseName("IX_IdempotencyKeys_ExpiresAtUtc");
    }
}

/// <summary>
/// Durable idempotency store: <c>messaging.IdempotencyKeys</c> is the source of truth, the cache only speeds up replays of completed keys.
/// Every operation runs in its own scope/transaction so it is independent of the command's unit of work (the behavior calls it before
/// and after the transaction, never inside it). Survives a cache flush or restart, which the cache-only store does not.
/// </summary>
public sealed class DurableIdempotencyStore<TContext> : IIdempotencyStore where TContext : BaseDbContext
{
    public const int MaxKeyLength = 128;

    private readonly IServiceScopeFactory _scopes;
    private readonly ICacheStore _cache;
    private readonly TimeProvider _clock;

    public DurableIdempotencyStore(IServiceScopeFactory scopes, ICacheStore cache, TimeProvider clock)
    {
        _scopes = scopes;
        _cache = cache;
        _clock = clock;
    }

    public async Task<IdempotencyEntry?> GetAsync(string scope, string key, CancellationToken ct = default)
    {
        key = Normalize(key);
        var cached = await _cache.GetJsonAsync<IdempotencyEntry>(CacheKey(scope, key), ct);
        if (cached is { Completed: true })
        {
            return cached;
        }

        var now = Now();
        using var serviceScope = _scopes.CreateScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<TContext>();
        var row = await db.Set<IdempotencyKeyRecord>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key && r.ExpiresAtUtc > now, ct);
        if (row is null)
        {
            return null;
        }

        var entry = ToEntry(row);
        if (entry.Completed)
        {
            await _cache.SetJsonAsync(CacheKey(scope, key), entry, row.ExpiresAtUtc - now, ct);
        }

        return entry;
    }

    public async Task<bool> TryBeginAsync(string scope, string key, string fingerprint, TimeSpan ttl, CancellationToken ct = default)
    {
        key = Normalize(key);
        var now = Now();
        using var serviceScope = _scopes.CreateScope();
        var db = serviceScope.ServiceProvider.GetRequiredService<TContext>();

        // An expired reservation no longer counts: clear it so the same key can be used again.
        await db.Set<IdempotencyKeyRecord>().Where(r => r.Scope == scope && r.Key == key && r.ExpiresAtUtc <= now).ExecuteDeleteAsync(ct);

        // The common replay is answered without provoking (and logging) a unique violation; the primary key still decides a true race below.
        if (await db.Set<IdempotencyKeyRecord>().AnyAsync(r => r.Scope == scope && r.Key == key, ct))
        {
            return false;
        }

        db.Set<IdempotencyKeyRecord>().Add(new IdempotencyKeyRecord
        {
            Scope = scope, Key = key, Fingerprint = fingerprint, CreatedAtUtc = now, ExpiresAtUtc = now + ttl
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (UniqueConstraintViolationException)
        {
            return false;
        }
    }

    public async Task CompleteAsync(string scope, string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct = default)
    {
        key = Normalize(key);
        var now = Now();
        using (var serviceScope = _scopes.CreateScope())
        {
            var db = serviceScope.ServiceProvider.GetRequiredService<TContext>();
            await db.Set<IdempotencyKeyRecord>().Where(r => r.Scope == scope && r.Key == key).ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Completed, entry.Completed)
                .SetProperty(r => r.IsSuccess, entry.IsSuccess)
                .SetProperty(r => r.PayloadJson, entry.PayloadJson)
                .SetProperty(r => r.ExpiresAtUtc, now + ttl), ct);
        }

        await _cache.SetJsonAsync(CacheKey(scope, key), entry, ttl, ct);
    }

    public async Task ReleaseAsync(string scope, string key, CancellationToken ct = default)
    {
        key = Normalize(key);
        using (var serviceScope = _scopes.CreateScope())
        {
            var db = serviceScope.ServiceProvider.GetRequiredService<TContext>();
            await db.Set<IdempotencyKeyRecord>().Where(r => r.Scope == scope && r.Key == key).ExecuteDeleteAsync(ct);
        }

        await _cache.RemoveAsync(CacheKey(scope, key), ct);
    }

    /// <summary>Deletes expired keys (called by the outbox housekeeping).</summary>
    public static Task<int> PurgeExpiredAsync(DbContext db, DateTime nowUtc, CancellationToken ct = default) =>
        db.Set<IdempotencyKeyRecord>().Where(r => r.ExpiresAtUtc <= nowUtc).ExecuteDeleteAsync(ct);

    private static IdempotencyEntry ToEntry(IdempotencyKeyRecord row) => new(row.Fingerprint, row.Completed, row.IsSuccess, row.PayloadJson, null);

    /// <summary>Client-chosen keys can be arbitrarily long; anything over the column width is stored as its SHA-256.</summary>
    private static string Normalize(string key) =>
        key.Length <= MaxKeyLength ? key : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static string CacheKey(string scope, string key) => $"idem:{scope}:{key}";

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;
}
