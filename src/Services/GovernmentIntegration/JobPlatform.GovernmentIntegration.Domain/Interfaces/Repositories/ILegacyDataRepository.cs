namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface ILegacyDataRepository
{
    Task<LegacyData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<LegacyData?> GetBySourceKeyAsync(SourceSystem sourceSystem, string sourceRecordId, CancellationToken ct = default);

    Task<IReadOnlyList<LegacyData>> ListByBatchAsync(Guid migrationBatchId, CancellationToken ct = default);

    void Add(LegacyData data);
}
