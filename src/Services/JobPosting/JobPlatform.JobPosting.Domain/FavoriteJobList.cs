using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobPosting.Domain;

/// <summary>AGG-18: one job seeker's heart-button list (US-3.2.2-03). At most one per owner.</summary>
public sealed class FavoriteJobList : AggregateRoot<Guid>
{
    public const int MaxEntries = 500;

    private readonly List<Guid> _jobPostingIds = new();

    private FavoriteJobList()
    {
    }

    public Guid OwnerAccountId { get; private set; }
    public IReadOnlyList<Guid> JobPostingIds => _jobPostingIds;

    public static FavoriteJobList CreateEmpty(Guid ownerAccountId) => new() { Id = Guid.NewGuid(), OwnerAccountId = ownerAccountId };

    /// <summary>AC-02: selecting again removes it (toggle), never duplicates. Raises <c>FavoriteJobListCreated</c> only when the job is added.</summary>
    public bool Toggle(Guid jobPostingId, Actor actor, DateTime nowUtc)
    {
        Check(Rules.PostingOwnerOnly(actor, OwnerAccountId, ErrorCodes.FavoriteForbidden));

        if (_jobPostingIds.Remove(jobPostingId))
        {
            return false;
        }

        Check(new BusinessRule(RuleCodes.FavoriteNotOwner, "The favourites list is full.", _jobPostingIds.Count >= MaxEntries,
            ErrorCodes.FavoriteForbidden, BusinessRuleKind.BusinessRule));
        _jobPostingIds.Add(jobPostingId);
        Raise(new FavoriteJobListCreatedDomainEvent(nowUtc, Id, actor.Id, jobPostingId));
        return true;
    }
}
