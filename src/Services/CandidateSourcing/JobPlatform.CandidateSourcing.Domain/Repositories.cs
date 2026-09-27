using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.CandidateSourcing.Domain.TalentPool;
using JobPlatform.CandidateSourcing.Domain.Threshold;

namespace JobPlatform.CandidateSourcing.Domain;

/// <summary>Aggregate-oriented repositories (foundation section 8). The read side (search, recommendations) never uses these - see the Application ports.</summary>
public interface ITalentPoolEntryRepository
{
    Task<TalentPoolEntry?> GetAsync(Guid employerAccountId, Guid candidateProfileId, Guid jobPostingId, CancellationToken ct = default);

    Task<TalentPoolEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TalentPoolEntry>> ListByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(TalentPoolEntry entry);
}

public interface IQualificationThresholdRepository
{
    Task<QualificationThreshold?> GetAsync(Guid employerAccountId, Guid jobPostingId, CancellationToken ct = default);

    void Add(QualificationThreshold threshold);
}

public interface ICandidateInsightRepository
{
    void Add(CandidateInsight insight);
}

public interface ICandidateProjectionRepository
{
    Task<CandidateProjection?> GetAsync(Guid profileId, CancellationToken ct = default);

    Task<CandidateProjection?> GetByOwnerAccountIdAsync(Guid ownerAccountId, CancellationToken ct = default);

    void Add(CandidateProjection projection);
}

public interface IVerifiedEmployerRepository
{
    Task<bool> IsVerifiedAsync(Guid employerAccountId, CancellationToken ct = default);

    Task<VerifiedEmployer?> GetAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(VerifiedEmployer employer);
}
