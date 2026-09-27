using JobPlatform.AiMatching.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence;

internal sealed class CandidateShortlistRepository(AiMatchingDbContext db) : ICandidateShortlistRepository
{
    public Task<CandidateShortlist?> GetAsync(Guid id, CancellationToken ct = default) => db.CandidateShortlists.FirstOrDefaultAsync(s => s.Id == id, ct);

    public void Add(CandidateShortlist shortlist) => db.CandidateShortlists.Add(shortlist);
}

internal sealed class KnownProfileRepository(AiMatchingDbContext db) : IKnownProfileRepository
{
    public Task<KnownProfile?> GetAsync(Guid profileId, CancellationToken ct = default) => db.KnownProfiles.FirstOrDefaultAsync(p => p.Id == profileId, ct);

    public Task<KnownProfile?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.KnownProfiles.FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public async Task<IReadOnlyList<KnownProfile>> ListActiveAsync(int skip, int take, CancellationToken ct = default) =>
        await db.KnownProfiles.AsNoTracking().Where(p => p.Standing == KnownStanding.Active).OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);

    public void Add(KnownProfile profile) => db.KnownProfiles.Add(profile);
}

internal sealed class KnownPostingRepository(AiMatchingDbContext db) : IKnownPostingRepository
{
    public Task<KnownPosting?> GetAsync(Guid postingId, CancellationToken ct = default) => db.KnownPostings.FirstOrDefaultAsync(p => p.Id == postingId, ct);

    public async Task<IReadOnlyList<KnownPosting>> ListActiveAsync(int skip, int take, CancellationToken ct = default) =>
        await db.KnownPostings.AsNoTracking().Where(p => !p.Suspended && p.Status.ToLower() == "active").OrderBy(p => p.Id).Skip(skip).Take(take).ToListAsync(ct);

    public void Add(KnownPosting posting) => db.KnownPostings.Add(posting);
}

internal sealed class WorkItemRepository(AiMatchingDbContext db) : IWorkItemRepository
{
    public async Task<bool> EnqueueAsync(WorkItemKind kind, Guid entityId, DateTime nowUtc, CancellationToken ct = default)
    {
        var pending = db.WorkItems.Local.Any(w => w.Kind == kind && w.EntityId == entityId && w.Status == WorkItemStatus.Pending)
                      || await db.WorkItems.AnyAsync(w => w.Kind == kind && w.EntityId == entityId && w.Status == WorkItemStatus.Pending, ct);
        if (pending)
        {
            return false;
        }

        db.WorkItems.Add(MatchingWorkItem.Enqueue(kind, entityId, nowUtc));
        return true;
    }

    public async Task<IReadOnlyList<MatchingWorkItem>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        await db.WorkItems.AsNoTracking().Where(w => w.Status == WorkItemStatus.Pending && w.NextAttemptUtc <= nowUtc).OrderBy(w => w.CreatedAtUtc).Take(take).ToListAsync(ct);

    public Task<MatchingWorkItem?> GetAsync(Guid id, CancellationToken ct = default) => db.WorkItems.FirstOrDefaultAsync(w => w.Id == id, ct);
}
