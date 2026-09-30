namespace JobPlatform.JobPosting.Application.Interfaces;

/// <summary>Cache-aside store for reference data (foundation section 10). Failures degrade to the database, never to an error.</summary>
public interface IJobPostingCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
