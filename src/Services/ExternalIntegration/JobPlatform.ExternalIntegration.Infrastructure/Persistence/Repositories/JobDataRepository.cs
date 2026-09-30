using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence.Repositories;

internal sealed class JobDataRepository(ExternalIntegrationDbContext db) : IJobDataRepository
{
    public Task<JobData?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.JobData.FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<JobData?> GetBySourceKeyAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default) =>
        db.JobData.FirstOrDefaultAsync(j => j.SourcePlatformId == sourcePlatformId && j.SourceJobId == sourceJobId, ct);

    public Task<JobData?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default) =>
        db.JobData.FirstOrDefaultAsync(j => j.PlatformJobId == platformJobId, ct);

    public void Add(JobData jobData) => db.JobData.Add(jobData);
}
