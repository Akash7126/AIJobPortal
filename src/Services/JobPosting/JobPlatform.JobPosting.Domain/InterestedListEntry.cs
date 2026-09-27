using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain;

/// <summary>AGG-20: a bookmark of a posting or a stored filter (US-3.2.3-01). Distinct from <see cref="FavoriteJobList"/> (handover Q-08).</summary>
public sealed class InterestedListEntry : AggregateRoot<Guid>
{
    private InterestedListEntry()
    {
        Reference = InterestedReference.ToPosting(Guid.Empty);
    }

    public Guid OwnerAccountId { get; private set; }
    public InterestedReference Reference { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>AC-02: not duplicated - the caller (application handler) must check <see cref="Matches"/> against the owner's existing entries first.</summary>
    public static InterestedListEntry Add(Guid ownerAccountId, InterestedReference reference, Actor actor, DateTime nowUtc)
    {
        Check(Rules.PostingOwnerOnly(actor, ownerAccountId, ErrorCodes.InterestedForbidden));
        var entry = new InterestedListEntry { Id = Guid.NewGuid(), OwnerAccountId = ownerAccountId, Reference = reference, CreatedAtUtc = nowUtc };
        entry.Raise(new InterestedListEntryCreatedDomainEvent(nowUtc, entry.Id, actor.Id, reference.Type));
        return entry;
    }

    public bool Matches(InterestedReference reference) => Equals(Reference, reference);
}
