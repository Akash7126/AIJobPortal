using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class JobPostAttributionRepository(ExternalIntegrationDbContext db) : IJobPostAttributionRepository
{
    public Task<JobPostAttribution?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default) =>
        db.JobPostAttributions.FirstOrDefaultAsync(a => a.PlatformJobId == platformJobId, ct);

    public void Add(JobPostAttribution attribution) => db.JobPostAttributions.Add(attribution);
}
