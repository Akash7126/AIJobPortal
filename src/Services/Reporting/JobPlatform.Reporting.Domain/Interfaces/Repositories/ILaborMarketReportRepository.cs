namespace JobPlatform.Reporting.Domain.Interfaces.Repositories;

public interface ILaborMarketReportRepository
{
    Task<LaborMarketReport?> GetByPeriodAsync(string period, CancellationToken ct = default);

    Task<IReadOnlyList<LaborMarketReport>> ListAsync(CancellationToken ct = default);

    void Add(LaborMarketReport report);
}
