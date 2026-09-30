namespace JobPlatform.JobPosting.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8). The read/search side never uses them (see IJobPostingSearchReadModel).</summary>
public interface IJobPostingRepository
{
    Task<JobPosting?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>INV-03: an identical draft by the same employer is updated, not duplicated (foundation section 8 unique index).</summary>
    Task<JobPosting?> GetDraftByHashAsync(Guid employerAccountId, string contentHash, CancellationToken ct = default);

    Task<JobPosting?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default);

    /// <summary>Active/paused postings whose deadline has passed and auto-close is on (US-3.2.1-01 AC-02, the auto-close job).</summary>
    Task<IReadOnlyList<JobPosting>> ListDueForExpiryAsync(DateTime nowUtc, int batchSize, CancellationToken ct = default);

    /// <summary>Active postings for saved-search match evaluation (US-3.2.2-04 AC-04).</summary>
    Task<JobPosting?> GetForMatchEvaluationAsync(Guid id, CancellationToken ct = default);

    void Add(JobPosting posting);
}
