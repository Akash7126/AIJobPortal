using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.Interfaces;

public interface IMatchingConfigurationProvider
{
    /// <summary>The current configuration snapshot (cache-aside; falls back to the database, then to the built-in defaults).</summary>
    Task<MatchingConfigSnapshot> GetAsync(CancellationToken ct = default);

    Task InvalidateAsync(CancellationToken ct = default);
}
