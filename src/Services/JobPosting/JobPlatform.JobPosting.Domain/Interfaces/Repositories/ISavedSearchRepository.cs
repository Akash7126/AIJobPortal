namespace JobPlatform.JobPosting.Domain.Interfaces.Repositories;

public interface ISavedSearchRepository
{
    Task<SavedSearch?> GetByHashAsync(Guid ownerAccountId, string criteriaHash, CancellationToken ct = default);

    Task<SavedSearch?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SavedSearch>> ListByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);

    /// <summary>Every saved search with notifications enabled (US-3.2.2-04 AC-04 match evaluation).</summary>
    Task<IReadOnlyList<SavedSearch>> ListNotifyingAsync(CancellationToken ct = default);

    void Add(SavedSearch search);

    void Remove(SavedSearch search);
}
