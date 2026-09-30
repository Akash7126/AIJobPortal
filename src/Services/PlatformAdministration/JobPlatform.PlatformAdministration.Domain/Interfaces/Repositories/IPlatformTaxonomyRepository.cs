using JobPlatform.PlatformAdministration.Domain.Taxonomy;

namespace JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;

public interface IPlatformTaxonomyRepository
{
    Task<PlatformTaxonomy?> GetByTypeAsync(string type, CancellationToken ct = default);

    void Add(PlatformTaxonomy taxonomy);

    /// <summary>Stores the snapshot of a saved version (immutable per version).</summary>
    void AddSnapshot(TaxonomySnapshot snapshot);
}
