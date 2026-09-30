using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class LegacyDataRepository(GovernmentIntegrationDbContext db) : ILegacyDataRepository
{
    public Task<LegacyData?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.LegacyData.FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<LegacyData?> GetBySourceKeyAsync(SourceSystem sourceSystem, string sourceRecordId, CancellationToken ct = default) =>
        db.LegacyData.FirstOrDefaultAsync(l => l.SourceSystem == sourceSystem && l.SourceRecordId == sourceRecordId, ct);

    public async Task<IReadOnlyList<LegacyData>> ListByBatchAsync(Guid migrationBatchId, CancellationToken ct = default) =>
        await db.LegacyData.Where(l => l.MigrationBatchId == migrationBatchId).ToListAsync(ct);

    public void Add(LegacyData data) => db.LegacyData.Add(data);
}
