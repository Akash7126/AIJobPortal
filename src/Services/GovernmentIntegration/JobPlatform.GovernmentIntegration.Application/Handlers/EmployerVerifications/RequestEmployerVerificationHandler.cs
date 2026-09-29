using JobPlatform.GovernmentIntegration.Application.Commands.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Application.DTOs.EmployerVerifications;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.EmployerVerifications;

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
