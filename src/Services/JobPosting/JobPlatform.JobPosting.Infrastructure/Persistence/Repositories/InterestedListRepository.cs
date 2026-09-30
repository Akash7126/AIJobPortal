using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobPosting.Infrastructure.Persistence.Repositories;

internal sealed class InterestedListRepository : IInterestedListRepository
{
    private readonly JobPostingDbContext _db;

    public InterestedListRepository(JobPostingDbContext db) => _db = db;

    public async Task<IReadOnlyList<InterestedListEntry>> ListByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        await _db.InterestedListEntries.Where(e => e.OwnerAccountId == ownerAccountId).ToListAsync(ct);

    public Task<InterestedListEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.InterestedListEntries.FirstOrDefaultAsync(e => e.Id == id, ct);

    public void Add(InterestedListEntry entry) => _db.InterestedListEntries.Add(entry);

    public void Remove(InterestedListEntry entry) => _db.InterestedListEntries.Remove(entry);
}
