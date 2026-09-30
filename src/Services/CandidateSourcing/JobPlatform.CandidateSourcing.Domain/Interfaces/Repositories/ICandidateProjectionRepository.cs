using JobPlatform.CandidateSourcing.Domain.Projection;

namespace JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;

public interface ICandidateProjectionRepository
{
    Task<CandidateProjection?> GetAsync(Guid profileId, CancellationToken ct = default);

    Task<CandidateProjection?> GetByOwnerAccountIdAsync(Guid ownerAccountId, CancellationToken ct = default);

    void Add(CandidateProjection projection);
}
