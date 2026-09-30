namespace JobPlatform.Reporting.Domain.Interfaces.Repositories;

public interface IReportTemplateRepository
{
    Task<ReportTemplate?> GetAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ReportTemplate>> ListAsync(CancellationToken ct = default);

    void Add(ReportTemplate template);
}
