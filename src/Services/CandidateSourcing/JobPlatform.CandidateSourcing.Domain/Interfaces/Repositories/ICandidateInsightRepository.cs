using JobPlatform.CandidateSourcing.Domain.Insight;

namespace JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;

public interface ICandidateInsightRepository
{
    void Add(CandidateInsight insight);
}
