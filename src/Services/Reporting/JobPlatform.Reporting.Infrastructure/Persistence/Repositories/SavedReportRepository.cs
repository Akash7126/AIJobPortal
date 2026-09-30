using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class SavedReportRepository(ReportingDbContext db) : ISavedReportRepository
{
    public Task<SavedReport?> GetAsync(Guid id, CancellationToken ct = default) => db.SavedReports.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<SavedReport>> ListOwnedAsync(Guid ownerId, CancellationToken ct = default) =>
        await db.SavedReports.Where(r => r.OwnerId == ownerId && !r.IsArchived).OrderByDescending(r => r.CreatedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<SavedReport>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        await db.SavedReports.Where(r => !r.IsArchived && r.RetainUntilUtc < nowUtc).OrderBy(r => r.RetainUntilUtc).Take(take).ToListAsync(ct);

    public void Add(SavedReport report) => db.SavedReports.Add(report);

    public void Remove(SavedReport report) => db.SavedReports.Remove(report);
}
