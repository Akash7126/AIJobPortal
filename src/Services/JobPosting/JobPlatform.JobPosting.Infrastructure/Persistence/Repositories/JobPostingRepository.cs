using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobPosting.Infrastructure.Persistence.Repositories;

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
