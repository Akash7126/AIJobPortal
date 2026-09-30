using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class ReportTemplateRepository(ReportingDbContext db) : IReportTemplateRepository
{
    public Task<ReportTemplate?> GetAsync(Guid id, CancellationToken ct = default) => db.ReportTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<ReportTemplate>> ListAsync(CancellationToken ct = default) => await db.ReportTemplates.OrderBy(t => t.Name).ToListAsync(ct);

    public void Add(ReportTemplate template) => db.ReportTemplates.Add(template);
}
