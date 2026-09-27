using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.JobSeekerProfile.Domain;

public enum ProfileVisibility
{
    Public,
    Private
}

public enum DeletionRequestState
{
    None,
    Requested,
    Queued,
    Accepted,
    Completed
}

/// <summary>AGG (proposed) PrivacySetting (handover section 3.6): 1:1 with a profile. No integration events - BC-03 owns AccountSuspended.</summary>
public sealed class PrivacySetting : Entity<Guid>
{
    private PrivacySetting()
    {
    }

    public Guid ProfileId { get; private set; }
    public ProfileVisibility Visibility { get; private set; } = ProfileVisibility.Private;

    /// <summary>Q-06: treated as its own explicit flag, separate from Visibility (a private candidate may still opt in to employer visibility).</summary>
    public bool PublicSharingActive { get; private set; }

    public DeletionRequestState DeletionState { get; private set; } = DeletionRequestState.None;
    public DateTime? DeactivationRequestedAtUtc { get; private set; }

    public static PrivacySetting CreateDefault(Guid profileId) => new() { Id = profileId, ProfileId = profileId };

    public void EnsureOwnedBy(Actor actor, Guid ownerAccountId) => Check(Rules.NotOwner(actor, ownerAccountId));

    public void SetVisibility(bool @public, bool publicSharingActive)
    {
        Visibility = @public ? ProfileVisibility.Public : ProfileVisibility.Private;
        PublicSharingActive = publicSharingActive;
    }

    public void RequestDeactivation(DateTime nowUtc) => DeactivationRequestedAtUtc = nowUtc;

    /// <summary>INV-13: a second request while one is already pending is queued, and the resubmission itself is a conflict (AC-03).</summary>
    public void RequestDeletion(DateTime nowUtc)
    {
        Check(new BusinessRule(RuleCodes.DeletionAlreadyPending, "A deletion request is already pending administrator review.",
            DeletionState is DeletionRequestState.Requested or DeletionRequestState.Queued, ErrorCodes.Conflict, BusinessRuleKind.Conflict));
        DeletionState = DeletionRequestState.Requested;
    }
}
