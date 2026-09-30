using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class JobDataMappingRepository(ExternalIntegrationDbContext db) : IJobDataMappingRepository
{
    public Task<JobDataMapping?> GetByIntegrationAsync(Guid integrationId, CancellationToken ct = default) =>
        db.JobDataMappings.FirstOrDefaultAsync(m => m.IntegrationId == integrationId, ct);

    public void Add(JobDataMapping mapping) => db.JobDataMappings.Add(mapping);
}
