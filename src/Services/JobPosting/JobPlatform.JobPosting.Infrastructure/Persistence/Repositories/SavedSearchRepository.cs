using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobPosting.Infrastructure.Persistence.Repositories;

internal sealed class SavedSearchRepository : ISavedSearchRepository
{
    private readonly JobPostingDbContext _db;

    public SavedSearchRepository(JobPostingDbContext db) => _db = db;

    public Task<SavedSearch?> GetByHashAsync(Guid ownerAccountId, string criteriaHash, CancellationToken ct = default) =>
        _db.SavedSearches.FirstOrDefaultAsync(s => s.OwnerAccountId == ownerAccountId && s.CriteriaHash == criteriaHash, ct);

    public Task<SavedSearch?> GetByIdAsync(Guid id, CancellationToken ct = default) => _db.SavedSearches.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SavedSearch>> ListByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        await _db.SavedSearches.Where(s => s.OwnerAccountId == ownerAccountId).ToListAsync(ct);

    public async Task<IReadOnlyList<SavedSearch>> ListNotifyingAsync(CancellationToken ct = default) =>
        await _db.SavedSearches.Where(s => s.NotifyOnMatch).ToListAsync(ct);

    public void Add(SavedSearch search) => _db.SavedSearches.Add(search);

    public void Remove(SavedSearch search) => _db.SavedSearches.Remove(search);
}
