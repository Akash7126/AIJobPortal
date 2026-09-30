namespace JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;

public interface IJobPreferenceRepository
{
    Task<JobPreference?> GetByProfileAsync(Guid profileId, CancellationToken ct = default);
    void Add(JobPreference preference);
}
