namespace JobPlatform.CandidateSourcing.Application.Interfaces;

/// <summary>Cache-aside store for the derived data this BC recomputes often (foundation section 10). Failures degrade to a fresh computation, never an error.</summary>
public interface ICandidateSourcingCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);
}
