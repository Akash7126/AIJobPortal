using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;
using JobPlatform.GovernmentIntegration.Application.Interfaces;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application.Handlers.Verifications;

internal sealed class RequestGovernmentVerificationHandler : ICommandHandler<RequestGovernmentVerificationCommand, Guid>
{
    private readonly IGovernmentVerificationDataRepository _data;
    private readonly IGovernmentDataAccessLogRepository _accessLog;
    private readonly GovernmentDataAccessPolicy _policy;
    private readonly IMolRegistryClient _mol;
    private readonly IPefClient _pef;
    private readonly IGovernmentDatabaseClient _govDb;
    private readonly TimeProvider _clock;

    public RequestGovernmentVerificationHandler(IGovernmentVerificationDataRepository data, IGovernmentDataAccessLogRepository accessLog,
        GovernmentDataAccessPolicy policy, IMolRegistryClient mol, IPefClient pef, IGovernmentDatabaseClient govDb, TimeProvider clock)
    {
        _data = data;
        _accessLog = accessLog;
        _policy = policy;
        _mol = mol;
        _pef = pef;
        _govDb = govDb;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(RequestGovernmentVerificationCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var subject = new Subject(request.SubjectType, request.SubjectId);

        // US-3.4.2-06: every access decision, allow and deny, is logged (AC-01).
        var decision = _policy.Authorise(request.RequestingComponent, request.Purpose);
        _accessLog.Add(new GovernmentDataAccessLogEntry(Guid.NewGuid(), now, request.RequestingComponent, request.Purpose,
            $"{request.SubjectType}:{request.SubjectId}", decision.IsAllowed ? "Allow" : "Deny", decision.ErrorCode));
        if (!decision.IsAllowed)
        {
            return Error.Forbidden(decision.ErrorCode!, "This component is not authorised to access government data for this purpose.");
        }

        var record = GovernmentVerificationData.Request(Guid.NewGuid(), subject, request.Source, request.Purpose, now);
        _data.Add(record);

        IGovernmentVerificationSourceClient client = request.Source switch
        {
            SourceSystem.MoL => _mol,
            SourceSystem.PEF => _pef,
            _ => _govDb
        };
        record.RecordAttempt();
        var result = await client.CheckSubjectAsync(subject, request.Purpose, ct);
        switch (result.Outcome)
        {
            case SourceCallOutcome.Match:
                record.RecordMatch((result.Fields ?? new Dictionary<string, string>()).Select(f => new VerifiedField(f.Key, f.Value)), now);
                break;
            case SourceCallOutcome.NoMatch:
                record.RecordNoMatch(now);
                break;
            default:
                record.RecordSourceUnavailable();
                break;
        }

        return record.Id;
    }
}
