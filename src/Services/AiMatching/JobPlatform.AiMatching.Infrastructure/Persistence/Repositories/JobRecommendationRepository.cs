using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AiMatching.Infrastructure.Persistence.Repositories;

internal sealed class JobRecommendationRepository(AiMatchingDbContext db) : IJobRecommendationRepository
{
    public Task<JobRecommendation?> GetLatestAsync(Guid profileId, CancellationToken ct = default) =>
        db.JobRecommendations.Where(r => r.ProfileId == profileId).OrderByDescending(r => r.ComputedAtUtc).FirstOrDefaultAsync(ct);

    public void Add(JobRecommendation recommendation) => db.JobRecommendations.Add(recommendation);
}
