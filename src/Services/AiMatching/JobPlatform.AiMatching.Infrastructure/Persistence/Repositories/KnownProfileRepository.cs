using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class KnownProfileRepository(AiMatchingDbContext db) : IKnownProfileRepository
{
    public Task<KnownProfile?> GetAsync(Guid profileId, CancellationToken ct = default) => db.KnownProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct);

    public Task<KnownProfile?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.KnownProfiles.FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public async Task<IReadOnlyList<KnownProfile>> ListActiveAsync(int skip, int take, CancellationToken ct = default) =>
        await db.KnownProfiles.AsNoTracking().Where(p => p.Standing == KnownStanding.Active).OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);

    public void Add(KnownProfile profile) => db.KnownProfiles.Add(profile);
}
