using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class KnownPostingRepository(AiMatchingDbContext db) : IKnownPostingRepository
{
    public Task<KnownPosting?> GetAsync(Guid postingId, CancellationToken ct = default) => db.KnownPostings.FirstOrDefaultAsync(p => p.Id == postingId, ct);

    public async Task<IReadOnlyList<KnownPosting>> ListActiveAsync(int skip, int take, CancellationToken ct = default) =>
        await db.KnownPostings.AsNoTracking().Where(p => !p.Suspended && p.Status.ToLower() == "active").OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);

    public void Add(KnownPosting posting) => db.KnownPostings.Add(posting);
}
