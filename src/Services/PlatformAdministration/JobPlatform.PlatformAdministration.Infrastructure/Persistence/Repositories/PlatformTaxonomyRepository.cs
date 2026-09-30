using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence.Repositories;

internal sealed class PlatformTaxonomyRepository(AdminDbContext db) : IPlatformTaxonomyRepository
{
    public Task<PlatformTaxonomy?> GetByTypeAsync(string type, CancellationToken ct = default) =>
        db.Taxonomies.Include(t => t.Nodes).FirstOrDefaultAsync(t => t.Type == type, ct);

    public void Add(PlatformTaxonomy taxonomy) => db.Taxonomies.Add(taxonomy);

    public void AddSnapshot(TaxonomySnapshot snapshot) => db.TaxonomyVersions.Add(snapshot);
}
