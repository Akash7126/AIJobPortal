namespace JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

public interface IJobPostAttributionRepository
{
    Task<JobPostAttribution?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default);

    void Add(JobPostAttribution attribution);
}
