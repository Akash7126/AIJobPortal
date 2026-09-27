using FluentValidation;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.GovernmentIntegration.Application;

// ---------------------------------------------------------------------- commands

/// <summary>US-3.1.2-03: employer submits their claim. Automatic matching runs synchronously in the same handler (deviation, see BC-01.md) -
/// this environment has no message-based internal-command dispatch beyond the outbox/inbox used for cross-BC events.</summary>
public sealed record RequestEmployerVerificationCommand(string RegistrationNumber, string VatNumber, string MobileNumber, string? IdempotencyKey)
    : EmployerCommand<EmployerVerificationView>, IIdempotentCommand;

public sealed class RequestEmployerVerificationValidator : AbstractValidator<RequestEmployerVerificationCommand>
{
    public RequestEmployerVerificationValidator()
    {
        RuleFor(c => c.RegistrationNumber).NotEmpty().MaximumLength(50).Matches("^[A-Za-z0-9\\-/]+$").WithErrorCode("VAL.RegistrationNumber.Invalid");
        RuleFor(c => c.VatNumber).NotEmpty().MaximumLength(50).WithErrorCode("VAL.VatNumber.Required");
        // Palestinian mobile ranges +970 59x/56x (proposed, handover section 7.3 "confirm"): kept permissive (E.164) beyond that prefix check.
        RuleFor(c => c.MobileNumber).NotEmpty().Matches("^\\+?[0-9]{7,15}$").WithErrorCode("VAL.MobileNumber.Invalid");
    }
}

internal sealed class RequestEmployerVerificationHandler : ICommandHandler<RequestEmployerVerificationCommand, EmployerVerificationView>
{
    private readonly IEmployerVerificationRepository _verifications;
    private readonly IKnownAccountRepository _knownAccounts;
    private readonly IMolRegistryClient _mol;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RequestEmployerVerificationHandler(IEmployerVerificationRepository verifications, IKnownAccountRepository knownAccounts,
        IMolRegistryClient mol, ICurrentUser user, TimeProvider clock)
    {
        _verifications = verifications;
        _knownAccounts = knownAccounts;
        _mol = mol;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<EmployerVerificationView>> Handle(RequestEmployerVerificationCommand request, CancellationToken ct)
    {
        var employerAccountId = _user.UserId!.Value;

        // INV-02: the account must be known and of actor type Employer, via the KnownAccounts replica (handover section 5.2, D-01).
        var known = await _knownAccounts.GetAsync(employerAccountId, ct);
        if (known is null || known.ActorType != ActorType.Employer)
        {
            return Error.Conflict(Domain.Common.ErrorCodes.AccountNotEmployer,
                "The employer account is not yet known to Government Integration. Try again shortly.");
        }

        if (await _verifications.ExistsActiveAsync(employerAccountId, ct))
        {
            return Error.Conflict(Domain.Common.ErrorCodes.AlreadyDecided, "An employer verification is already in progress for this account.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var submission = new Submission(request.RegistrationNumber, request.VatNumber, request.MobileNumber);
        var verification = EmployerVerification.Request(Guid.NewGuid(), employerAccountId, submission);
        _verifications.Add(verification);

        await RunAutomaticMatchAsync(verification, now, ct);
        return ToView(verification);
    }

    /// <summary>US-3.1.2-03 AC-01..03: a single automatic attempt against MoL. IMolRegistryClient resolves its own 30s x 3-retry policy
    /// internally (handover section 4.2) and reports the final outcome.</summary>
    private async Task RunAutomaticMatchAsync(EmployerVerification verification, DateTime now, CancellationToken ct)
    {
        var result = await _mol.VerifyEmployerAsync(verification.Submission, ct);
        switch (result.Outcome)
        {
            case SourceCallOutcome.Match:
                verification.RecordAttempt(SourceSystem.MoL, AttemptOutcome.Match, null, now);
                verification.MarkMatched(SourceSystem.MoL, now);
                break;
            case SourceCallOutcome.NoMatch:
                verification.RecordAttempt(SourceSystem.MoL, AttemptOutcome.NoMatch, null, now);
                verification.EscalateToManualReview("Automatic match not found.");
                break;
            default:
                verification.RecordAttempt(SourceSystem.MoL, AttemptOutcome.Timeout, result.ErrorCode ?? Domain.Common.ErrorCodes.EmployerVerificationUpstreamTimeout, now);
                verification.EscalateToManualReview("Automatic verification timed out after 3 attempts.");
                break;
        }
    }

    internal static EmployerVerificationView ToView(EmployerVerification v) => new(
        v.Id, v.EmployerAccountId, v.State.ToString(), v.Method.ToString(), v.AttemptCount, v.DecidedBy, v.DecidedAtUtc, v.FailureReason,
        v.Attempts.Select(a => new VerificationAttemptView(a.AttemptNo, a.Source.ToString(), a.Outcome.ToString(), a.ErrorCode, a.StartedAtUtc)).ToArray(),
        v.RowVersion);
}

public enum ManualDecision
{
    Approve,
    Reject
}

/// <summary>US-3.1.2-03 (MoL decision). Handover Q-03 (proposed): reuses the Administrator role for the MoL reviewer, and adds a Reject path.</summary>
public sealed record DecideEmployerVerificationManuallyCommand(Guid EmployerVerificationId, ManualDecision Decision, string? Reason)
    : AdminCommand<Unit>;

public sealed class DecideEmployerVerificationManuallyValidator : AbstractValidator<DecideEmployerVerificationManuallyCommand>
{
    public DecideEmployerVerificationManuallyValidator()
    {
        RuleFor(c => c.EmployerVerificationId).NotEmpty().WithErrorCode("VAL.EmployerVerificationId.Required");
        RuleFor(c => c.Decision).IsInEnum().WithErrorCode("VAL.Decision.Invalid");
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(500).When(c => c.Decision == ManualDecision.Reject).WithErrorCode("VAL.Reason.Required");
    }
}

internal sealed class DecideEmployerVerificationManuallyHandler : ICommandHandler<DecideEmployerVerificationManuallyCommand, Unit>
{
    private readonly IEmployerVerificationRepository _verifications;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public DecideEmployerVerificationManuallyHandler(IEmployerVerificationRepository verifications, ICurrentUser user, TimeProvider clock)
    {
        _verifications = verifications;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(DecideEmployerVerificationManuallyCommand request, CancellationToken ct)
    {
        var verification = await _verifications.GetByIdAsync(request.EmployerVerificationId, ct);
        if (verification is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer verification was not found.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var reviewerId = _user.UserId!.Value;
        if (request.Decision == ManualDecision.Approve)
        {
            verification.ApproveManually(reviewerId, now);
        }
        else
        {
            verification.RejectManually(reviewerId, request.Reason!, now);
        }

        return Result.Success();
    }
}

// ---------------------------------------------------------------------- queries

public sealed record GetEmployerVerificationQuery(Guid EmployerVerificationId) : IQuery<EmployerVerificationView>, IAuthorizedRequest
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer, ActorType.Administrator };

    public string ForbiddenErrorCode => Domain.Common.ErrorCodes.Forbidden;
}

internal sealed class GetEmployerVerificationHandler : IQueryHandler<GetEmployerVerificationQuery, EmployerVerificationView>
{
    private readonly IGovernmentIntegrationReadStore _store;
    private readonly ICurrentUser _user;

    public GetEmployerVerificationHandler(IGovernmentIntegrationReadStore store, ICurrentUser user)
    {
        _store = store;
        _user = user;
    }

    public async Task<Result<EmployerVerificationView>> Handle(GetEmployerVerificationQuery request, CancellationToken ct)
    {
        var view = await _store.GetEmployerVerificationAsync(request.EmployerVerificationId, ct);
        if (view is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The employer verification was not found.");
        }

        if (_user.ActorType == ActorType.Employer && view.EmployerAccountId != _user.UserId)
        {
            return Error.Forbidden(Domain.Common.ErrorCodes.Forbidden, "You may only view your own employer verification.");
        }

        return view;
    }
}

public sealed record ListPendingManualReviewQuery(int Page = 1, int PageSize = 20) : AdminQuery<PagedResult<EmployerVerificationView>>;

internal sealed class ListPendingManualReviewHandler : IQueryHandler<ListPendingManualReviewQuery, PagedResult<EmployerVerificationView>>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public ListPendingManualReviewHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<PagedResult<EmployerVerificationView>>> Handle(ListPendingManualReviewQuery request, CancellationToken ct) =>
        await _store.ListPendingManualReviewAsync(new PageRequest(request.Page, request.PageSize), ct);
}
