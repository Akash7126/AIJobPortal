namespace JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

public interface IJobDataRepository
{
    Task<JobData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<JobData?> GetBySourceKeyAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default);

    Task<JobData?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default);

    void Add(JobData jobData);
}
