namespace JobPlatform.JobPosting.Domain.Interfaces.Repositories;

public interface IFavoriteJobListRepository
{
    Task<FavoriteJobList?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);

    void Add(FavoriteJobList list);
}
