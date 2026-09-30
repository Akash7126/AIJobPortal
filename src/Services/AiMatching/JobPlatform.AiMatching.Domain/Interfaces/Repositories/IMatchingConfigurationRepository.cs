namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8). The unit of work commits; the read side uses IMatchReadStore instead.</summary>
public interface IMatchingConfigurationRepository
{
    /// <summary>The singleton configuration, or null before it is seeded.</summary>
    Task<MatchingConfiguration?> GetCurrentAsync(CancellationToken ct = default);

    void Add(MatchingConfiguration configuration);
}
