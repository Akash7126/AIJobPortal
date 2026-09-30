using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.GovernmentIntegration.Infrastructure.Persistence.Repositories;

internal sealed class DataQualityRepository(GovernmentIntegrationDbContext db) : IDataQualityRepository
{
    public Task<DataQuality?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.DataQuality.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<DataQuality?> GetByBatchAsync(Guid batchId, CancellationToken ct = default) =>
        db.DataQuality.Where(d => d.BatchId == batchId).OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);

    public Task<bool> HasRunningForBatchAsync(Guid batchId, CancellationToken ct = default) =>
        db.DataQuality.AnyAsync(d => d.BatchId == batchId && d.Status == DataQualityStatus.Running, ct);

    public void Add(DataQuality dataQuality) => db.DataQuality.Add(dataQuality);
}
