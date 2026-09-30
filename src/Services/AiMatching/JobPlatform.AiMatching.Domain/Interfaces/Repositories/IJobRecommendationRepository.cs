namespace JobPlatform.AiMatching.Domain.Interfaces.Repositories;

public interface IJobRecommendationRepository
{
    Task<JobRecommendation?> GetLatestAsync(Guid profileId, CancellationToken ct = default);

    void Add(JobRecommendation recommendation);
}
