using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.EmployerOnboarding.Domain;

/// <summary>One row per employer account: which BC-05 verification event last changed the badge, and when (handover 3.3 AC-04 traceability).</summary>
public sealed class BadgeAuditEntry : Entity<Guid>
{
    private BadgeAuditEntry()
    {
    }

    internal BadgeAuditEntry(Guid id, Guid causedByMessageId, string change, DateTime atUtc)
    {
        Id = id;
        CausedByMessageId = causedByMessageId;
        Change = change;
        AtUtc = atUtc;
    }

    public Guid CausedByMessageId { get; private set; }

    public string Change { get; private set; } = string.Empty;

    public DateTime AtUtc { get; private set; }
}

/// <summary>
/// Proposed read-model/aggregate (handover section 3.3): the employer's admission and government-verification standing, and the "Verified Employer"
/// badge that other BCs (BC-09, BC-11, BC-06) display. AdmissionApproved is set by EmployerRegistration.Approve; IsVerified is set only by the
/// EmployerVerificationApproved inbox handler, which ignores a stale/out-of-order aggregateVersion (INV: idempotent, monotonic).
/// </summary>
public sealed class EmployerStanding : AggregateRoot<Guid>
{
    private readonly List<BadgeAuditEntry> _badgeAudit = new();

    private EmployerStanding()
    {
    }

    public Guid EmployerAccountId { get; private set; }

    public bool AdmissionApproved { get; private set; }

    public bool IsVerified { get; private set; }

    public DateTime? VerifiedAtUtc { get; private set; }

    /// <summary>Highest EmployerVerificationApproved.AggregateVersion applied so far; a redelivery or an older event is ignored.</summary>
    public long LastVerificationEventVersion { get; private set; }

    public IReadOnlyCollection<BadgeAuditEntry> BadgeAudit => _badgeAudit;

    public static EmployerStanding OpenFor(Guid id, Guid employerAccountId) => new() { Id = id, EmployerAccountId = employerAccountId };

    public void MarkAdmissionApproved(DateTime nowUtc)
    {
        if (AdmissionApproved)
        {
            return;
        }

        AdmissionApproved = true;
        Raise(new EmployerStandingChangedDomainEvent(EmployerAccountId, nowUtc));
    }

    /// <summary>Idempotent: a duplicate or out-of-order EmployerVerificationApproved (lower or equal version) changes nothing.</summary>
    public void MarkVerified(Guid causingMessageId, long eventVersion, DateTime nowUtc)
    {
        if (eventVersion <= LastVerificationEventVersion)
        {
            return;
        }

        LastVerificationEventVersion = eventVersion;
        if (!IsVerified)
        {
            IsVerified = true;
            VerifiedAtUtc = nowUtc;
            _badgeAudit.Add(new BadgeAuditEntry(Guid.NewGuid(), causingMessageId, "VerifiedBadgeGranted", nowUtc));
            Raise(new EmployerStandingChangedDomainEvent(EmployerAccountId, nowUtc));
        }
    }

    public string? BadgeLabel(SharedKernel.Common.Enums.Language language) =>
        IsVerified ? (language == SharedKernel.Common.Enums.Language.Ar ? "صاحب عمل موثّق" : "Verified Employer") : null;
}
