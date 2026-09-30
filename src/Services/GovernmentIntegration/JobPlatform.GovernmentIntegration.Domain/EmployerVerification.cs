using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;

namespace JobPlatform.GovernmentIntegration.Domain;

public enum VerificationState
{
    Pending,
    PendingManualReview,
    Verified,

    /// <summary>Proposed (handover Q-03): the source only defines approval; a reject path is added so a MoL reviewer can close a case.</summary>
    Rejected
}

public enum VerificationMethod
{
    Automatic,
    ManualMoL
}

public enum AttemptOutcome
{
    Match,
    NoMatch,
    Timeout,
    Error
}

/// <summary>The claim an employer submits to be checked (handover section 3.1). GAP-003: the checked set is not necessarily limited to the three
/// illustrative fields, so <see cref="AdditionalFields"/> keeps the value object extensible without a schema change.</summary>
public sealed class Submission : ValueObject
{
    public Submission(string registrationNumber, string vatNumber, string mobileNumber, IReadOnlyDictionary<string, string>? additionalFields = null)
    {
        RegistrationNumber = registrationNumber;
        VatNumber = vatNumber;
        MobileNumber = mobileNumber;
        AdditionalFields = additionalFields ?? new Dictionary<string, string>();
    }

    public string RegistrationNumber { get; }
    public string VatNumber { get; }
    public string MobileNumber { get; }
    public IReadOnlyDictionary<string, string> AdditionalFields { get; }

    /// <summary>INV-01 precondition: registration number, VAT number and mobile number must all be present.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(RegistrationNumber) && !string.IsNullOrWhiteSpace(VatNumber) && !string.IsNullOrWhiteSpace(MobileNumber);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RegistrationNumber;
        yield return VatNumber;
        yield return MobileNumber;
        foreach (var pair in AdditionalFields.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            yield return pair.Key;
            yield return pair.Value;
        }
    }
}

/// <summary>One automatic (or manual) attempt to match the submission against a source (handover section 3.1, child collection).</summary>
public sealed class VerificationAttempt : Entity<Guid>
{
    private VerificationAttempt()
    {
    }

    internal VerificationAttempt(Guid id, int attemptNo, SourceSystem source, AttemptOutcome outcome, string? errorCode, DateTime startedAtUtc)
    {
        Id = id;
        AttemptNo = attemptNo;
        Source = source;
        Outcome = outcome;
        ErrorCode = errorCode;
        StartedAtUtc = startedAtUtc;
    }

    public int AttemptNo { get; private set; }
    public SourceSystem Source { get; private set; }
    public AttemptOutcome Outcome { get; private set; }
    public string? ErrorCode { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
}

/// <summary>
/// AGG-06: the automatic-then-manual process confirming an employer's legitimacy (handover section 3.1, story US-3.1.2-03). The system actor id
/// (<see cref="SystemActorId"/>) is published as the actor on an automatic match, since no human decided it.
/// </summary>
public sealed class EmployerVerification : AggregateRoot<Guid>
{
    /// <summary>A-02-004: at most 3 automatic attempts per source call before INV-05 requires escalation.</summary>
    public const int MaxAutomaticAttempts = 3;

    public static readonly Guid SystemActorId = Guid.Empty;

    private readonly List<VerificationAttempt> _attempts = new();

    private EmployerVerification()
    {
    }

    public Guid EmployerAccountId { get; private set; }
    public Submission Submission { get; private set; } = default!;
    public VerificationState State { get; private set; }
    public VerificationMethod Method { get; private set; }
    public int AttemptCount { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public IReadOnlyCollection<VerificationAttempt> Attempts => _attempts;

    /// <summary>INV-05: once <see cref="AttemptCount"/> reaches <see cref="MaxAutomaticAttempts"/> on timeouts, the application must escalate
    /// to manual review rather than leave the verification pending indefinitely (AC-03).</summary>
    public bool AutomaticAttemptsExhausted => AttemptCount >= MaxAutomaticAttempts;

    /// <summary>INV-01: the submission must be complete. INV-02 (the account must exist and be an Employer) is checked by the application
    /// against the KnownAccounts replica before this factory is called - the aggregate itself has no dependency on account state.</summary>
    public static EmployerVerification Request(Guid id, Guid employerAccountId, Submission submission)
    {
        Check(new BusinessRule(RuleCodes.EmployerVerificationSubmissionIncomplete,
            "Registration number, VAT number and mobile number are all required.", !submission.IsComplete,
            ErrorCodes.SubmissionIncomplete, BusinessRuleKind.BusinessRule));

        return new EmployerVerification
        {
            Id = id,
            EmployerAccountId = employerAccountId,
            Submission = submission,
            State = VerificationState.Pending,
            Method = VerificationMethod.Automatic
        };
    }

    /// <summary>Records the outcome of one automatic (or manual) source call. Attempt numbers are strictly increasing (A-02-004).</summary>
    public void RecordAttempt(SourceSystem source, AttemptOutcome outcome, string? errorCode, DateTime nowUtc)
    {
        var attemptNo = _attempts.Count + 1;
        _attempts.Add(new VerificationAttempt(Guid.NewGuid(), attemptNo, source, outcome, errorCode, nowUtc));
        AttemptCount = attemptNo;
    }

    /// <summary>INV-03: only from Pending. Publishes EmployerVerificationApproved with the system as actor (AC-01, AC-04).</summary>
    public void MarkMatched(SourceSystem source, DateTime nowUtc)
    {
        Check(AlreadyDecidedRule());
        State = VerificationState.Verified;
        DecidedAtUtc = nowUtc;
        Raise(new EmployerVerificationApprovedDomainEvent(Id, EmployerAccountId, SystemActorId, VerificationMethod.Automatic.ToString(), nowUtc));
    }

    /// <summary>Only from Pending: automatic no-match confirmed, or retries exhausted on timeout (AC-02, AC-03). No event is published here -
    /// escalation itself is internal; EmployerVerificationApproved is only published once a decision is reached.</summary>
    public void EscalateToManualReview(string reason)
    {
        Check(AlreadyDecidedRule());
        State = VerificationState.PendingManualReview;
        Method = VerificationMethod.ManualMoL;
        FailureReason = reason;
    }

    /// <summary>INV-04: only from PendingManualReview. Publishes EmployerVerificationApproved with the reviewer as actor (AC-01, AC-04).</summary>
    public void ApproveManually(Guid reviewerId, DateTime nowUtc)
    {
        Check(NotInManualReviewRule());
        State = VerificationState.Verified;
        DecidedBy = reviewerId;
        DecidedAtUtc = nowUtc;
        Raise(new EmployerVerificationApprovedDomainEvent(Id, EmployerAccountId, reviewerId, VerificationMethod.ManualMoL.ToString(), nowUtc));
    }

    /// <summary>Proposed (handover Q-03): only from PendingManualReview. No integration event is catalogued for a rejection.</summary>
    public void RejectManually(Guid reviewerId, string reason, DateTime nowUtc)
    {
        Check(NotInManualReviewRule());
        State = VerificationState.Rejected;
        DecidedBy = reviewerId;
        DecidedAtUtc = nowUtc;
        FailureReason = reason;
    }

    private IBusinessRule AlreadyDecidedRule() =>
        new BusinessRule(RuleCodes.EmployerVerificationAlreadyDecided, "This verification has already been decided.",
            State != VerificationState.Pending, ErrorCodes.AlreadyDecided, BusinessRuleKind.Conflict);

    private IBusinessRule NotInManualReviewRule() =>
        new BusinessRule(RuleCodes.EmployerVerificationNotInManualReview, "This decision requires the verification to be pending manual review.",
            State != VerificationState.PendingManualReview, ErrorCodes.NotInManualReview, BusinessRuleKind.Conflict);
}
