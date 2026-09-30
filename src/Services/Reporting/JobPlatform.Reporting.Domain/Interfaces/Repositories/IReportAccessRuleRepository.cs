namespace JobPlatform.Reporting.Domain.Interfaces.Repositories;

public interface IReportAccessRuleRepository
{
    Task<IReadOnlyList<ReportAccessRule>> ListAsync(CancellationToken ct = default);

    Task<ReportAccessRule?> GetByRoleAsync(string role, CancellationToken ct = default);

    void Add(ReportAccessRule rule);

    void AddDecision(ReportAccessDecision decision);
}
