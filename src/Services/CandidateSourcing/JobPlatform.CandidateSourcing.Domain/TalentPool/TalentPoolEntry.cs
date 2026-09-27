using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.CandidateSourcing.Domain.TalentPool;

public sealed record TalentPoolEntryCreatedDomainEvent(Guid TalentPoolEntryId, Guid JobPostingId, Guid EmployerAccountId, Guid ActorId, DateTime OccurredOnUtc)
    : DomainEvent(OccurredOnUtc);

/// <summary>AGG-24 (story US-3.3.3-07): an employer's manually saved candidate for a job posting. No copy of candidate data - privacy is enforced at read time.</summary>
public sealed class TalentPoolEntry : AggregateRoot<Guid>
{
    private TalentPoolEntry()
    {
    }

    public Guid EmployerAccountId { get; private set; }

    public Guid CandidateProfileId { get; private set; }

    public Guid JobPostingId { get; private set; }

    public string? Note { get; private set; }

    public DateTime AddedAtUtc { get; private set; }

    public bool Removed { get; private set; }

    /// <summary>
    /// INV-01: at most one entry per (employer, candidate, posting) - the application must re-use an existing, non-removed entry rather than
    /// create a duplicate (AC-02). INV-03: the candidate must currently be visible - candidateVisible is the application's privacy-policy
    /// evaluation of the candidate at the time of the call; the domain only enforces the resulting boolean.
    /// </summary>
    public static TalentPoolEntry Create(Guid employerAccountId, Guid candidateProfileId, Guid jobPostingId, string? note, DateTime nowUtc, Guid actorId,
        bool candidateVisible)
    {
        Rules.EnsureForbidden(!candidateVisible, RuleCodes.TalentPoolNotVisible, "The candidate is not currently visible to employers.");
        var entry = new TalentPoolEntry
        {
            Id = Guid.NewGuid(),
            EmployerAccountId = employerAccountId,
            CandidateProfileId = candidateProfileId,
            JobPostingId = jobPostingId,
            Note = note,
            AddedAtUtc = nowUtc
        };
        entry.Raise(new TalentPoolEntryCreatedDomainEvent(entry.Id, jobPostingId, employerAccountId, actorId, nowUtc));
        return entry;
    }

    /// <summary>INV-02: owner-only.</summary>
    public void EnsureOwnedBy(Guid callerAccountId) => Check(Rules.OwnerOnly(EmployerAccountId, callerAccountId));

    public void UpdateNote(string? note, Guid callerAccountId)
    {
        EnsureOwnedBy(callerAccountId);
        Note = note;
    }

    public void Remove(Guid callerAccountId)
    {
        EnsureOwnedBy(callerAccountId);
        Removed = true;
    }
}
