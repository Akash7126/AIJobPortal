using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;
using JobPlatform.GovernmentIntegration.Application.Interfaces;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Verifications;

internal sealed class RequestIdentityVerificationHandler : ICommandHandler<RequestIdentityVerificationCommand, Guid>
{
    private readonly IIdentityVerificationRepository _repository;
    private readonly IGovernmentDataAccessLogRepository _accessLog;
    private readonly GovernmentDataAccessPolicy _policy;
    private readonly IGovernmentIdClient _client;
    private readonly TimeProvider _clock;

    public RequestIdentityVerificationHandler(IIdentityVerificationRepository repository, IGovernmentDataAccessLogRepository accessLog,
        GovernmentDataAccessPolicy policy, IGovernmentIdClient client, TimeProvider clock)
    {
        _repository = repository;
        _accessLog = accessLog;
        _policy = policy;
        _client = client;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(RequestIdentityVerificationCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var decision = _policy.Authorise(request.RequestingComponent, AccessPurpose.IdentityVerification);
        _accessLog.Add(new GovernmentDataAccessLogEntry(Guid.NewGuid(), now, request.RequestingComponent, AccessPurpose.IdentityVerification,
            request.SubjectId.ToString(), decision.IsAllowed ? "Allow" : "Deny", decision.ErrorCode));
        if (!decision.IsAllowed)
        {
            return Error.Forbidden(decision.ErrorCode!, "This component is not authorised to request identity verification.");
        }

        var subject = new Subject(request.SubjectType, request.SubjectId);
        var claim = new IdentityClaim(request.NationalIdReference, request.FullName, request.DateOfBirth);
        var verification = IdentityVerificationData.Request(Guid.NewGuid(), subject, claim, now);
        _repository.Add(verification);

        verification.RecordAttempt();
        var result = await _client.CheckAsync(claim, ct);
        switch (result.Outcome)
        {
            case SourceCallOutcome.Match:
                verification.MarkVerified(now);
                break;
            case SourceCallOutcome.NoMatch when result.Ambiguous:
                // Handover Q-05 (proposed): several candidate matches ⇒ Unverified(AMBIGUOUS) with manual follow-up, never picked automatically.
                verification.MarkUnverified(UnverifiedReasons.Ambiguous, now);
                break;
            case SourceCallOutcome.NoMatch:
                verification.MarkUnverified(UnverifiedReasons.NoMatch, now);
                break;
            default:
                verification.RecordSystemUnavailable();
                break;
        }

        return verification.Id;
    }
}
