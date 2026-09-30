namespace JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;

public interface IDataQualityRepository
{
    Task<DataQuality?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<DataQuality?> GetByBatchAsync(Guid batchId, CancellationToken ct = default);

    /// <summary>INV-13: a new cleansing run must not start while one is already Running for the same batch.</summary>
    Task<bool> HasRunningForBatchAsync(Guid batchId, CancellationToken ct = default);

    void Add(DataQuality dataQuality);
}
