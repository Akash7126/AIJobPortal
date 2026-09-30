namespace JobPlatform.Reporting.Domain.Interfaces.Repositories;

public interface ISavedReportRepository
{
    Task<SavedReport?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SavedReport>> ListOwnedAsync(Guid ownerId, CancellationToken ct = default);

    Task<IReadOnlyList<SavedReport>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default);

    void Add(SavedReport report);

    void Remove(SavedReport report);
}
