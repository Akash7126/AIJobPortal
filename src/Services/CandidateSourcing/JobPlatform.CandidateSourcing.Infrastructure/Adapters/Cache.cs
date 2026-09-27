using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.CandidateSourcing.Application;

namespace JobPlatform.CandidateSourcing.Infrastructure.Adapters;

/// <summary>Cache-aside over the shared cache store (Redis or in-memory - foundation section 10). A cache failure degrades to a fresh computation.</summary>
internal sealed class CandidateSourcingCache(ICacheStore store) : ICandidateSourcingCache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => store.GetJsonAsync<T>(key, ct);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => store.SetJsonAsync(key, value, ttl, ct);

    public Task RemoveAsync(string key, CancellationToken ct = default) => store.RemoveAsync(key, ct);
}
