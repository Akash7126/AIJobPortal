using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;

internal sealed class ExportJobRepository : IExportJobRepository
{
    private readonly AuditDbContext _db;

    public ExportJobRepository(AuditDbContext db) => _db = db;

    public Task<ExportJob?> GetAsync(Guid id, CancellationToken ct = default) => _db.ExportJobs.FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<ExportJob?> FindInProgressAsync(Guid requestedBy, ReportType type, string parametersHash, CancellationToken ct = default) =>
        _db.ExportJobs.FirstOrDefaultAsync(j => j.RequestedBy == requestedBy && j.ReportType == type && j.ParametersHash == parametersHash
                                                && (j.Status == ExportStatus.Queued || j.Status == ExportStatus.Generating), ct);

    public async Task<IReadOnlyList<ExportJob>> ListQueuedAsync(int take, CancellationToken ct = default) =>
        await _db.ExportJobs.Where(j => j.Status == ExportStatus.Queued).OrderBy(j => j.RequestedAtUtc).Take(take).ToListAsync(ct);

    public void Add(ExportJob job) => _db.ExportJobs.Add(job);
}
