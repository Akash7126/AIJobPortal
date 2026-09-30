using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class ReportAccessRuleRepository(ReportingDbContext db) : IReportAccessRuleRepository
{
    public async Task<IReadOnlyList<ReportAccessRule>> ListAsync(CancellationToken ct = default) => await db.ReportAccessRules.AsNoTracking().OrderBy(r => r.Role).ToListAsync(ct);

    public Task<ReportAccessRule?> GetByRoleAsync(string role, CancellationToken ct = default) => db.ReportAccessRules.FirstOrDefaultAsync(r => r.Role == role, ct);

    public void Add(ReportAccessRule rule) => db.ReportAccessRules.Add(rule);

    public void AddDecision(ReportAccessDecision decision) => db.ReportAccessDecisions.Add(decision);
}
