using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;

internal sealed class JobStatusHistoryRepository : IJobStatusHistoryRepository
{
    private readonly AuditDbContext _db;

    public JobStatusHistoryRepository(AuditDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid sourceMessageId, CancellationToken ct = default) =>
        _db.JobStatusHistory.AnyAsync(h => h.SourceMessageId == sourceMessageId, ct);

    public void Add(JobStatusHistoryEntry entry) => _db.JobStatusHistory.Add(entry);
}
