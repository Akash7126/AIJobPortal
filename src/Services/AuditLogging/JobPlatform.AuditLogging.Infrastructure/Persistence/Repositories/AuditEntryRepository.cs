using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class AuditEntryRepository : IAuditEntryRepository
{
    private readonly AuditDbContext _db;

    public AuditEntryRepository(AuditDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string sourceBc, Guid sourceMessageId, AuditCategory category, CancellationToken ct = default) =>
        _db.AuditEntries.AnyAsync(e => e.SourceBc == sourceBc && e.SourceMessageId == sourceMessageId && e.Category == category, ct);

    public void Add(AuditEntry entry) => _db.AuditEntries.Add(entry);

    public async Task<IReadOnlyList<AuditEntry>> ListExpiredAsync(DateTime nowUtc, int take, CancellationToken ct = default) =>
        await _db.AuditEntries.Where(e => !e.IsArchived && e.RetainUntilUtc < nowUtc).OrderBy(e => e.RetainUntilUtc).Take(take).ToListAsync(ct);
}
