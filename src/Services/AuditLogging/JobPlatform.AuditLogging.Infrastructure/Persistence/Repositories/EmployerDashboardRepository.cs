using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;

internal sealed class EmployerDashboardRepository : IEmployerDashboardRepository
{
    private readonly AuditDbContext _db;

    public EmployerDashboardRepository(AuditDbContext db) => _db = db;

    public Task<EmployerDashboard?> GetAsync(Guid employerId, CancellationToken ct = default) =>
        _db.EmployerDashboards.FirstOrDefaultAsync(d => d.Id == employerId, ct);

    public void Add(EmployerDashboard dashboard) => _db.EmployerDashboards.Add(dashboard);
}
