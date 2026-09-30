namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface IWorkItemRepository
{
    /// <summary>Enqueues unless an identical (kind, entity) item is already pending (idempotent).</summary>
    Task<bool> EnqueueAsync(WorkItemKind kind, Guid entityId, DateTime nowUtc, CancellationToken ct = default);

    Task<IReadOnlyList<MatchingWorkItem>> ListDueAsync(DateTime nowUtc, int take, CancellationToken ct = default);

    Task<MatchingWorkItem?> GetAsync(Guid id, CancellationToken ct = default);
}
