using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Verifications;

internal sealed class RequestEducationalCredentialVerificationHandler : ICommandHandler<RequestEducationalCredentialVerificationCommand, Guid>
{
    private readonly IEducationalCredentialVerificationRepository _repository;
    private readonly IGovernmentDataAccessLogRepository _accessLog;
    private readonly GovernmentDataAccessPolicy _policy;
    private readonly IEducationalInstitutionClient _client;
    private readonly TimeProvider _clock;

    public RequestEducationalCredentialVerificationHandler(IEducationalCredentialVerificationRepository repository,
        IGovernmentDataAccessLogRepository accessLog, GovernmentDataAccessPolicy policy, IEducationalInstitutionClient client, TimeProvider clock)
    {
        _repository = repository;
        _accessLog = accessLog;
        _policy = policy;
        _client = client;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(RequestEducationalCredentialVerificationCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var decision = _policy.Authorise(request.RequestingComponent, AccessPurpose.CredentialVerification);
        _accessLog.Add(new GovernmentDataAccessLogEntry(Guid.NewGuid(), now, request.RequestingComponent, AccessPurpose.CredentialVerification,
            request.SubjectId.ToString(), decision.IsAllowed ? "Allow" : "Deny", decision.ErrorCode));
        if (!decision.IsAllowed)
        {
            return Error.Forbidden(decision.ErrorCode!, "This component is not authorised to request credential verification.");
        }

        var credential = new Credential(request.Institution, request.CredentialName, request.Year);
        var verification = EducationalCredentialVerification.Request(Guid.NewGuid(), request.SubjectId, credential, now);
        _repository.Add(verification);

        verification.RecordAttempt();
        var result = await _client.CheckAsync(credential, ct);
        switch (result.Outcome)
        {
            case SourceCallOutcome.Match:
                verification.MarkVerified(now);
                break;
            case SourceCallOutcome.NoMatch:
                verification.MarkUnverified(now);
                break;
            default:
                verification.RecordInstitutionUnavailable();
                break;
        }

        return verification.Id;
    }
}
