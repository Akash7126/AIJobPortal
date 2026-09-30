using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class ReportExportRepository(ReportingDbContext db) : IReportExportRepository
{
    public Task<ReportExport?> GetAsync(Guid id, CancellationToken ct = default) => db.ReportExports.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<ReportExport?> FindRunningAsync(Guid requestedBy, string parametersHash, CancellationToken ct = default) =>
        db.ReportExports.FirstOrDefaultAsync(e => e.RequestedBy == requestedBy && e.ParametersHash == parametersHash
                                                  && (e.Status == ExportStatus.Queued || e.Status == ExportStatus.Generating), ct);

    public async Task<IReadOnlyList<ReportExport>> ListQueuedAsync(int take, CancellationToken ct = default) =>
        await db.ReportExports.Where(e => e.Status == ExportStatus.Queued).OrderBy(e => e.RequestedAtUtc).Take(take).ToListAsync(ct);

    public Task<ReportExportFile?> GetFileAsync(Guid exportId, CancellationToken ct = default) => db.ReportExportFiles.FirstOrDefaultAsync(f => f.Id == exportId, ct);

    public void Add(ReportExport export) => db.ReportExports.Add(export);

    public void AddFile(ReportExportFile file) => db.ReportExportFiles.Add(file);
}
