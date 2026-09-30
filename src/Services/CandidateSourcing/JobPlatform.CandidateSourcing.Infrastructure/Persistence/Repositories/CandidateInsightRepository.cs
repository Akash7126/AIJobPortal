using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;

namespace JobPlatform.CandidateSourcing.Infrastructure.Persistence.Repositories;

internal sealed class CandidateInsightRepository(CandidateSourcingDbContext db) : ICandidateInsightRepository
{
    public void Add(CandidateInsight insight) => db.CandidateInsights.Add(insight);
}
