using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class LaborMarketReportRepository(ReportingDbContext db) : ILaborMarketReportRepository
{
    public Task<LaborMarketReport?> GetByPeriodAsync(string period, CancellationToken ct = default) => db.LaborMarketReports.FirstOrDefaultAsync(r => r.Period == period, ct);

    public async Task<IReadOnlyList<LaborMarketReport>> ListAsync(CancellationToken ct = default) => await db.LaborMarketReports.OrderByDescending(r => r.Period).ToListAsync(ct);

    public void Add(LaborMarketReport report) => db.LaborMarketReports.Add(report);
}
