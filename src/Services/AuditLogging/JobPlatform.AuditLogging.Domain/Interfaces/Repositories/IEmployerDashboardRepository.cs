namespace JobPlatform.AuditLogging.Domain.Interfaces.Repositories;

public interface IEmployerDashboardRepository
{
    Task<EmployerDashboard?> GetAsync(Guid employerId, CancellationToken ct = default);

    void Add(EmployerDashboard dashboard);
}
