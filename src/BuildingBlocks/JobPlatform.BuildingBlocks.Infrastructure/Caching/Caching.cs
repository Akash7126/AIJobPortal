using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobPlatform.BuildingBlocks.Infrastructure.Diagnostics;
using JobPlatform.SharedKernel.Application.Ports;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace JobPlatform.BuildingBlocks.Infrastructure.Caching;

public sealed class CacheOptions
{
    /// <summary>"Redis" or "InMemory".</summary>
    public string Provider { get; set; } = "InMemory";

    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>Key prefix, convention "&lt;env&gt;:&lt;bc-slug&gt;" (foundation section 10).</summary>
    public string KeyPrefix { get; set; } = "dev:service";
}

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

public static class CacheStoreExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static async Task<T?> GetJsonAsync<T>(this ICacheStore store, string key, CancellationToken ct = default)
    {
        var raw = await store.GetAsync(key, ct);
        var tag = new KeyValuePair<string, object?>("kind", key.Split(':')[0]);
        if (raw is null)
        {
            BuildingBlockTelemetry.CacheMisses.Add(1, tag);
            return default;
        }

        BuildingBlockTelemetry.CacheHits.Add(1, tag);
        return JsonSerializer.Deserialize<T>(raw, Json);
    }

    public static Task SetJsonAsync<T>(this ICacheStore store, string key, T value, TimeSpan ttl, CancellationToken ct = default) =>
        store.SetAsync(key, JsonSerializer.Serialize(value, Json), ttl, ct);
}

public sealed class InMemoryCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, Entry> _values = new();
    private readonly ConcurrentDictionary<string, SetEntry> _sets = new();
    private readonly object _gate = new();
    private readonly TimeProvider _clock;
    private readonly string _prefix;

    public InMemoryCacheStore(TimeProvider clock, IOptions<CacheOptions> options)
    {
        _clock = clock;
        _prefix = options.Value.KeyPrefix;
    }

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Live(Key(key))?.Value);
        }
    }

    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _values[Key(key)] = new Entry(value, Now() + ttl);
        }

        return Task.CompletedTask;
    }

    public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (Live(Key(key)) is not null)
            {
                return Task.FromResult(false);
            }

            _values[Key(key)] = new Entry(value, Now() + ttl);
            return Task.FromResult(true);
        }
    }

    public Task<bool> RemoveAsync(string key, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_values.TryRemove(Key(key), out _) | _sets.TryRemove(Key(key), out _));
        }
    }

    public Task<long> IncrementAsync(string key, TimeSpan ttlOnCreate, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var k = Key(key);
            var current = Live(k);
            if (current is null)
            {
                _values[k] = new Entry("1", Now() + ttlOnCreate);
                return Task.FromResult(1L);
            }

            var next = long.Parse(current.Value, System.Globalization.CultureInfo.InvariantCulture) + 1;
            _values[k] = current with { Value = next.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            return Task.FromResult(next);
        }
    }

    public Task<bool> RefreshTtlAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var k = Key(key);
            var current = Live(k);
            if (current is null)
            {
                return Task.FromResult(false);
            }

            _values[k] = current with { ExpiresAt = Now() + ttl };
            return Task.FromResult(true);
        }
    }

    public Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var current = Live(Key(key));
            return Task.FromResult(current is null ? (TimeSpan?)null : current.ExpiresAt - Now());
        }
    }

    public Task SetAddAsync(string setKey, string member, TimeSpan ttl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var k = Key(setKey);
            var set = _sets.TryGetValue(k, out var existing) && existing.ExpiresAt > Now() ? existing : new SetEntry(new HashSet<string>(), Now() + ttl);
            set.Members.Add(member);
            _sets[k] = set with { ExpiresAt = Now() + ttl };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<string>> SetMembersAsync(string setKey, CancellationToken ct = default)
    {
        lock (_gate)
        {
            IReadOnlyCollection<string> members = _sets.TryGetValue(Key(setKey), out var set) && set.ExpiresAt > Now()
                ? set.Members.ToArray()
                : Array.Empty<string>();
            return Task.FromResult(members);
        }
    }

    public Task SetRemoveAsync(string setKey, string member, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_sets.TryGetValue(Key(setKey), out var set))
            {
                set.Members.Remove(member);
            }
        }

        return Task.CompletedTask;
    }

    private string Key(string key) => $"{_prefix}:{key}";

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    private Entry? Live(string key)
    {
        if (_values.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > Now())
            {
                return entry;
            }

            _values.TryRemove(key, out _);
        }

        return null;
    }

    private sealed record Entry(string Value, DateTime ExpiresAt);

    private sealed record SetEntry(HashSet<string> Members, DateTime ExpiresAt);
}

public sealed class RedisCacheStore : ICacheStore
{
    private const string IncrementScript =
        "local c = redis.call('INCR', KEYS[1]) if c == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end return c";

    private readonly IConnectionMultiplexer _redis;
    private readonly string _prefix;

    public RedisCacheStore(IConnectionMultiplexer redis, IOptions<CacheOptions> options)
    {
        _redis = redis;
        _prefix = options.Value.KeyPrefix;
    }

    private IDatabase Db => _redis.GetDatabase();

    private RedisKey Key(string key) => $"{_prefix}:{key}";

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var value = await Db.StringGetAsync(Key(key));
        return value.HasValue ? value.ToString() : null;
    }

    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) =>
        Db.StringSetAsync(Key(key), value, ttl);

    public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken ct = default) =>
        Db.StringSetAsync(Key(key), value, ttl, When.NotExists);

    public Task<bool> RemoveAsync(string key, CancellationToken ct = default) => Db.KeyDeleteAsync(Key(key));

    public async Task<long> IncrementAsync(string key, TimeSpan ttlOnCreate, CancellationToken ct = default)
    {
        var result = await Db.ScriptEvaluateAsync(IncrementScript, new[] { Key(key) }, new RedisValue[] { (long)ttlOnCreate.TotalMilliseconds });
        return (long)result;
    }

    public Task<bool> RefreshTtlAsync(string key, TimeSpan ttl, CancellationToken ct = default) => Db.KeyExpireAsync(Key(key), ttl);

    public Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken ct = default) => Db.KeyTimeToLiveAsync(Key(key));

    public async Task SetAddAsync(string setKey, string member, TimeSpan ttl, CancellationToken ct = default)
    {
        await Db.SetAddAsync(Key(setKey), member);
        await Db.KeyExpireAsync(Key(setKey), ttl);
    }

    public async Task<IReadOnlyCollection<string>> SetMembersAsync(string setKey, CancellationToken ct = default) =>
        (await Db.SetMembersAsync(Key(setKey))).Select(v => v.ToString()).ToArray();

    public Task SetRemoveAsync(string setKey, string member, CancellationToken ct = default) => Db.SetRemoveAsync(Key(setKey), member);
}

/// <summary>Rate-limit counters on top of the cache store (Redis or in-memory), foundation section 10.</summary>
public sealed class CacheRateLimiter : IRateLimiter
{
    private readonly ICacheStore _cache;

    public CacheRateLimiter(ICacheStore cache) => _cache = cache;

    public async Task<RateLimitDecision> HitAsync(string key, int permits, TimeSpan window, CancellationToken ct = default)
    {
        var count = await _cache.IncrementAsync(Key(key), window, ct);
        var allowed = count <= permits;
        var retryAfter = allowed ? TimeSpan.Zero : await _cache.GetTimeToLiveAsync(Key(key), ct) ?? window;
        return new RateLimitDecision(allowed, count, retryAfter);
    }

    public async Task<long> CountAsync(string key, CancellationToken ct = default) =>
        long.TryParse(await _cache.GetAsync(Key(key), ct), out var count) ? count : 0;

    public Task ResetAsync(string key, CancellationToken ct = default) => _cache.RemoveAsync(Key(key), ct);

    private static string Key(string key) => $"rl:{key}";
}

/// <summary>Idempotency-Key fast path (foundation section 10). Entries expire; the aggregate's own rules remain the final guard.</summary>
public sealed class CacheIdempotencyStore : IIdempotencyStore
{
    private readonly ICacheStore _cache;

    public CacheIdempotencyStore(ICacheStore cache) => _cache = cache;

    public Task<IdempotencyEntry?> GetAsync(string scope, string key, CancellationToken ct = default) =>
        _cache.GetJsonAsync<IdempotencyEntry>(Key(scope, key), ct);

    public async Task<bool> TryBeginAsync(string scope, string key, string fingerprint, TimeSpan ttl, CancellationToken ct = default)
    {
        var pending = new IdempotencyEntry(fingerprint, false, false, null, null);
        return await _cache.SetIfNotExistsAsync(Key(scope, key), JsonSerializer.Serialize(pending), ttl, ct);
    }

    public Task CompleteAsync(string scope, string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken ct = default) =>
        _cache.SetJsonAsync(Key(scope, key), entry, ttl, ct);

    public Task ReleaseAsync(string scope, string key, CancellationToken ct = default) => _cache.RemoveAsync(Key(scope, key), ct);

    private static string Key(string scope, string key) => $"idem:{scope}:{key}";
}
