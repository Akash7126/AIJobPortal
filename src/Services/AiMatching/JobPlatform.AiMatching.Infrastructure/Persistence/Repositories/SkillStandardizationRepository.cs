using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class SkillStandardizationRepository(AiMatchingDbContext db) : ISkillStandardizationRepository
{
    public Task<SkillStandardization?> GetByResumeParsedDataAsync(Guid resumeParsedDataId, CancellationToken ct = default) =>
        db.SkillStandardizations.Include(s => s.Mappings).FirstOrDefaultAsync(s => s.ResumeParsedDataId == resumeParsedDataId, ct);

    public async Task<IReadOnlyList<SkillStandardization>> ListNotOnTaxonomyVersionAsync(string taxonomyVersion, int take, CancellationToken ct = default) =>
        await db.SkillStandardizations.Include(s => s.Mappings).Where(s => s.TaxonomyVersion != taxonomyVersion).OrderBy(s => s.UpdatedAtUtc).Take(take).ToListAsync(ct);

    public void Add(SkillStandardization standardization) => db.SkillStandardizations.Add(standardization);
}
