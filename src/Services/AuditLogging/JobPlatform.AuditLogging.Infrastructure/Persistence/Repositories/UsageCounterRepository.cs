using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;

internal sealed class UsageCounterRepository : IUsageCounterRepository
{
    private readonly AuditDbContext _db;

    public UsageCounterRepository(AuditDbContext db) => _db = db;

    public Task<IntegrationUsageDaily?> GetAsync(Guid partnerId, DateOnly day, CancellationToken ct = default) =>
        _db.IntegrationUsageDaily.FirstOrDefaultAsync(u => u.PartnerId == partnerId && u.Day == day, ct);

    public void Add(IntegrationUsageDaily counter) => _db.IntegrationUsageDaily.Add(counter);
}
