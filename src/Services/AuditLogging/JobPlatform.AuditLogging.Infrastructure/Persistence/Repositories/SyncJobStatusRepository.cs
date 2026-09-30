using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;

internal sealed class SyncJobStatusRepository : ISyncJobStatusRepository
{
    private readonly AuditDbContext _db;

    public SyncJobStatusRepository(AuditDbContext db) => _db = db;

    public Task<SyncJobStatus?> GetAsync(string platformJobId, CancellationToken ct = default) =>
        _db.SyncJobStatuses.FirstOrDefaultAsync(s => s.Id == platformJobId, ct);

    public void Add(SyncJobStatus status) => _db.SyncJobStatuses.Add(status);
}
