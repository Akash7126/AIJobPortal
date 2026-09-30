using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.Interfaces;

/// <summary>BC-08 skills taxonomy (cached, a running job keeps the version it started with).</summary>
public interface ISkillTaxonomyProvider
{
    Task<SkillTaxonomy> GetAsync(string? version, CancellationToken ct = default);

    /// <summary>Evicts the cached taxonomy (PlatformTaxonomyUpdated).</summary>
    Task InvalidateAsync(CancellationToken ct = default);
}
