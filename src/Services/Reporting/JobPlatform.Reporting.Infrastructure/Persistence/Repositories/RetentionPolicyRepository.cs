using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Reporting.Infrastructure.Persistence.Repositories;

// Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).

internal sealed class RetentionPolicyRepository(ReportingDbContext db) : IRetentionPolicyRepository
{
    public Task<ActivityLogRetentionPolicy?> GetAsync(CancellationToken ct = default) =>
        db.RetentionPolicies.FirstOrDefaultAsync(p => p.Id == ActivityLogRetentionPolicy.SingletonId, ct);

    public void Add(ActivityLogRetentionPolicy policy) => db.RetentionPolicies.Add(policy);
}
