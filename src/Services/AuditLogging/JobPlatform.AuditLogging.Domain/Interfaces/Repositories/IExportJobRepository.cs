namespace JobPlatform.AuditLogging.Domain.Interfaces.Repositories;

public interface IExportJobRepository
{
    Task<ExportJob?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The Queued or Generating job with the same administrator, type and parameter hash (INV-04), if any.</summary>
    Task<ExportJob?> FindInProgressAsync(Guid requestedBy, ReportType type, string parametersHash, CancellationToken ct = default);

    Task<IReadOnlyList<ExportJob>> ListQueuedAsync(int take, CancellationToken ct = default);

    void Add(ExportJob job);
}
