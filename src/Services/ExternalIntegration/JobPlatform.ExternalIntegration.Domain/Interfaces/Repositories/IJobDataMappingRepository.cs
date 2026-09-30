namespace JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

public interface IJobDataMappingRepository
{
    Task<JobDataMapping?> GetByIntegrationAsync(Guid integrationId, CancellationToken ct = default);

    void Add(JobDataMapping mapping);
}
