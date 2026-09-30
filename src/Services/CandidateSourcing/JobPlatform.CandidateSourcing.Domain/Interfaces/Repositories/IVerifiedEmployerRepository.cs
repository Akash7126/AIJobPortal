using JobPlatform.CandidateSourcing.Domain.Projection;

namespace JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;

public interface IVerifiedEmployerRepository
{
    Task<bool> IsVerifiedAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<VerifiedEmployer?> GetAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(VerifiedEmployer employer);
}
