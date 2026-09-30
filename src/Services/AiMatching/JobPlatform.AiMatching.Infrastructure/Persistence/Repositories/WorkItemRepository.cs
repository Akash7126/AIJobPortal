using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

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
