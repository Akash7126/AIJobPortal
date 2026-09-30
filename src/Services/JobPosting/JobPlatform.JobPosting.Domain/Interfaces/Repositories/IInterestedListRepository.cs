namespace JobPlatform.JobPosting.Domain.Interfaces.Repositories;

public interface IInterestedListRepository
{
    Task<IReadOnlyList<InterestedListEntry>> ListByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);

    Task<InterestedListEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    void Add(InterestedListEntry entry);

    void Remove(InterestedListEntry entry);
}
