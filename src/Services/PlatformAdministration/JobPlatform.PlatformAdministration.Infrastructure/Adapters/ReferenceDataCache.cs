using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.PlatformAdministration.Application;
using Microsoft.Extensions.Logging;

namespace JobPlatform.PlatformAdministration.Infrastructure.Adapters;

/// <summary>Cache-aside adapter over the shared cache store (Redis or in-memory). A cache failure never fails a request: it degrades to the database (foundation section 10).</summary>
internal sealed class ReferenceDataCache : IReferenceDataCache
{
    private readonly ICacheStore _store;
    private readonly ILogger<ReferenceDataCache> _logger;

    public ReferenceDataCache(ICacheStore store, ILogger<ReferenceDataCache> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            return await _store.GetJsonAsync<T>(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache read failed for {Key}; falling back to the database", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            await _store.SetJsonAsync(key, value, ttl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _store.RemoveAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Cache eviction failed for {Key}", key);
        }
    }
}
