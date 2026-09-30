using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobPosting.Infrastructure.Persistence.Repositories;

internal sealed class FavoriteJobListRepository : IFavoriteJobListRepository
{
    private readonly JobPostingDbContext _db;

    public FavoriteJobListRepository(JobPostingDbContext db) => _db = db;

    public Task<FavoriteJobList?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        _db.FavoriteJobLists.FirstOrDefaultAsync(f => f.OwnerAccountId == ownerAccountId, ct);

    public void Add(FavoriteJobList list) => _db.FavoriteJobLists.Add(list);
}
