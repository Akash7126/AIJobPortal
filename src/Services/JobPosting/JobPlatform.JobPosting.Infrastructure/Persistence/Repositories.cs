using JobPlatform.JobPosting.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobPosting.Infrastructure.Persistence;

internal sealed class JobPostingRepository : IJobPostingRepository
{
    private readonly JobPostingDbContext _db;

    public JobPostingRepository(JobPostingDbContext db) => _db = db;

    public Task<Domain.JobPosting?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.JobPostings.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Domain.JobPosting?> GetDraftByHashAsync(Guid employerAccountId, string contentHash, CancellationToken ct = default) =>
        _db.JobPostings.FirstOrDefaultAsync(p => p.EmployerAccountId == employerAccountId && p.ContentHash == contentHash
            && p.Status == JobPostingStatus.Draft, ct);

    public Task<Domain.JobPosting?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default) =>
        _db.JobPostings.FirstOrDefaultAsync(p => p.Source.PlatformJobId == platformJobId, ct);

    public async Task<IReadOnlyList<Domain.JobPosting>> ListDueForExpiryAsync(DateTime nowUtc, int batchSize, CancellationToken ct = default) =>
        await _db.JobPostings
            .Where(p => (p.Status == JobPostingStatus.Active || p.Status == JobPostingStatus.Paused) && p.Deadline.AutoClose
                && p.Deadline.DateUtc <= nowUtc)
            .Take(batchSize)
            .ToListAsync(ct);

    public Task<Domain.JobPosting?> GetForMatchEvaluationAsync(Guid id, CancellationToken ct = default) =>
        _db.JobPostings.FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Add(Domain.JobPosting posting) => _db.JobPostings.Add(posting);
}

internal sealed class FavoriteJobListRepository : IFavoriteJobListRepository
{
    private readonly JobPostingDbContext _db;

    public FavoriteJobListRepository(JobPostingDbContext db) => _db = db;

    public Task<FavoriteJobList?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        _db.FavoriteJobLists.FirstOrDefaultAsync(f => f.OwnerAccountId == ownerAccountId, ct);

    public void Add(FavoriteJobList list) => _db.FavoriteJobLists.Add(list);
}

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
