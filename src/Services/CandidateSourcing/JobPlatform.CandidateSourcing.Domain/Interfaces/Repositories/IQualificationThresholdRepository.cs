using JobPlatform.CandidateSourcing.Domain.Threshold;

namespace JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;

public interface IQualificationThresholdRepository
{
    Task<QualificationThreshold?> GetAsync(Guid employerAccountId, Guid jobPostingId, CancellationToken ct = default);

    void Add(QualificationThreshold threshold);
}
