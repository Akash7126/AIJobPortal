using JobPlatform.CandidateSourcing.Domain.TalentPool;

namespace JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8). The read side (search, recommendations) never uses these - see the Application ports.</summary>
public interface ITalentPoolEntryRepository
{
    Task<TalentPoolEntry?> GetAsync(Guid employerAccountId, Guid candidateProfileId, Guid jobPostingId, CancellationToken ct = default);

    Task<TalentPoolEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TalentPoolEntry>> ListByEmployerAsync(Guid employerAccountId, CancellationToken ct = default);

    void Add(TalentPoolEntry entry);
}
