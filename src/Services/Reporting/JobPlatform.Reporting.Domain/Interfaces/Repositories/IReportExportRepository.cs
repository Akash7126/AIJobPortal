namespace JobPlatform.Reporting.Domain.Interfaces.Repositories;

public interface IReportExportRepository
{
    Task<ReportExport?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The Queued or Generating export with the same administrator and parameter hash (INV-06), if any.</summary>
    Task<ReportExport?> FindRunningAsync(Guid requestedBy, string parametersHash, CancellationToken ct = default);

    Task<IReadOnlyList<ReportExport>> ListQueuedAsync(int take, CancellationToken ct = default);

    Task<ReportExportFile?> GetFileAsync(Guid exportId, CancellationToken ct = default);

    void Add(ReportExport export);

    void AddFile(ReportExportFile file);
}
