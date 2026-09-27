using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.Offerings;

public enum JobOfferingStatus
{
    Active,
    Inactive
}

/// <summary>Why an offering is inactive. Removal has no catalogued semantics (Q-04): it is a suspension that is recorded as removed.</summary>
public enum ModerationKind
{
    Suspended,
    Removed
}

/// <summary>
/// AGG-16 JobOffering (Id = JobPostingId): a job posting seen from the oversight perspective (story US-3.1.4-09). It becomes eligible for
/// oversight when BC-09 announces the posting; suspension is enforced by BC-09 when it consumes JobOfferingSuspended.
/// </summary>
public sealed class JobOffering : AggregateRoot<Guid>
{
    private JobOffering()
    {
        Title = string.Empty;
    }

    public Guid EmployerId { get; private set; }

    public string Title { get; private set; }

    public JobOfferingStatus Status { get; private set; }

    public ModerationKind? Moderation { get; private set; }

    public Guid? SuspendedBy { get; private set; }

    public DateTime? SuspendedAtUtc { get; private set; }

    public string? Reason { get; private set; }

    public DateTime RegisteredAtUtc { get; private set; }

    /// <summary>The offering becomes eligible for oversight (idempotent per posting id: the handler skips a known id).</summary>
    public static JobOffering Register(Guid jobPostingId, Guid employerId, string title, DateTime nowUtc) => new()
    {
        Id = jobPostingId,
        EmployerId = employerId,
        Title = title,
        Status = JobOfferingStatus.Active,
        RegisteredAtUtc = nowUtc
    };

    /// <summary>INV-07: an already inactive offering cannot be suspended again (E-AUM-STATE-INACTIVE). Administrator only (E-AUM-FORBIDDEN).</summary>
    public void Suspend(Actor actor, string? reason, DateTime nowUtc) => Moderate(ModerationKind.Suspended, actor, reason, nowUtc);

    /// <summary>Proposed (Q-04): removes the offering from the platform. Same guards and the same published event as a suspension.</summary>
    public void Remove(Actor actor, string? reason, DateTime nowUtc) => Moderate(ModerationKind.Removed, actor, reason, nowUtc);

    private void Moderate(ModerationKind kind, Actor actor, string? reason, DateTime nowUtc)
    {
        Check(Rules.AdminOnly(actor));
        Check(new BusinessRule(RuleCodes.OfferingAlreadyInactive, "The job offering is already inactive.", Status == JobOfferingStatus.Inactive,
            ErrorCodes.StateInactive, BusinessRuleKind.Conflict));
        Rules.EnsureValid(string.IsNullOrWhiteSpace(reason), RuleCodes.OfferingReasonRequired, "A reason is required.", "reason");

        Status = JobOfferingStatus.Inactive;
        Moderation = kind;
        SuspendedBy = actor.Id;
        SuspendedAtUtc = nowUtc;
        Reason = reason!.Trim();
        Raise(new JobOfferingSuspendedDomainEvent(Id, actor.Id, Reason, kind, nowUtc));
    }
}
