namespace JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;

/// <summary>Low-level key/value + counter + set operations. Redis in production, in-memory fallback for local runs and tests. Never the source of truth.</summary>
public interface ICacheStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>SET NX with expiry. True when the key was created.</summary>
    Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Atomically increments a counter; the TTL is applied when the counter is created.</summary>
    Task<long> IncrementAsync(string key, TimeSpan ttlOnCreate, CancellationToken ct = default);

    Task<bool> RefreshTtlAsync(string key, TimeSpan ttl, CancellationToken ct = default);

    Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken ct = default);

    Task SetAddAsync(string setKey, string member, TimeSpan ttl, CancellationToken ct = default);

    Task<IReadOnlyCollection<string>> SetMembersAsync(string setKey, CancellationToken ct = default);

    Task SetRemoveAsync(string setKey, string member, CancellationToken ct = default);
}
