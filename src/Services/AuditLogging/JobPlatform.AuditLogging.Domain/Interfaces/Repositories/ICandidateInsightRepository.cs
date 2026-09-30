namespace JobPlatform.AuditLogging.Domain.Interfaces.Repositories;

public interface ICandidateInsightRepository
{
    Task<CandidateInsightRecord?> GetAsync(Guid insightId, CancellationToken ct = default);

    void Add(CandidateInsightRecord insight);
}
