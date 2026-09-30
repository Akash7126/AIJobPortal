using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class PlatformEntityRecordRepository(AdminDbContext db) : IPlatformEntityRecordRepository
{
    public Task<PlatformEntityRecord?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.PlatformEntityRecords.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExistsByKeyAsync(PlatformEntityType type, string identityKey, CancellationToken ct = default) =>
        db.PlatformEntityRecords.AnyAsync(r => r.EntityType == type && r.IdentityKey == identityKey, ct);

    public void Add(PlatformEntityRecord record) => db.PlatformEntityRecords.Add(record);
}
