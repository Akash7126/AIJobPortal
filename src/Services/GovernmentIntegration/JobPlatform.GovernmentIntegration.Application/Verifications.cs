using FluentValidation;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application;

// ============================================================ US-3.4.2-01 GovernmentVerificationData

public sealed record RequestGovernmentVerificationCommand(string RequestingComponent, SubjectType SubjectType, Guid SubjectId, SourceSystem Source,
    AccessPurpose Purpose) : ServiceCommand<Guid>;

public sealed class RequestGovernmentVerificationValidator : AbstractValidator<RequestGovernmentVerificationCommand>
{
    public RequestGovernmentVerificationValidator()
    {
        RuleFor(c => c.RequestingComponent).NotEmpty().WithErrorCode("VAL.RequestingComponent.Required");
        RuleFor(c => c.SubjectType).IsInEnum().WithErrorCode("VAL.SubjectType.Invalid");
        RuleFor(c => c.SubjectId).NotEmpty().WithErrorCode("VAL.SubjectId.Required");
        RuleFor(c => c.Source).IsInEnum().WithErrorCode("VAL.Source.Invalid");
        RuleFor(c => c.Purpose).IsInEnum().WithErrorCode("VAL.Purpose.Invalid");
    }
}

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

// ============================================================ US-3.4.2-03 EducationalCredentialVerification

public sealed record RequestEducationalCredentialVerificationCommand(string RequestingComponent, Guid SubjectId, string Institution, string CredentialName,
    int Year) : ServiceCommand<Guid>;

public sealed class RequestEducationalCredentialVerificationValidator : AbstractValidator<RequestEducationalCredentialVerificationCommand>
{
    public RequestEducationalCredentialVerificationValidator()
    {
        RuleFor(c => c.SubjectId).NotEmpty().WithErrorCode("VAL.SubjectId.Required");
        RuleFor(c => c.Institution).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Institution.Required");
        RuleFor(c => c.CredentialName).NotEmpty().MaximumLength(200).WithErrorCode("VAL.Credential.Required");
        RuleFor(c => c.Year).InclusiveBetween(1950, DateTime.UtcNow.Year).WithErrorCode("VAL.Year.OutOfRange");
    }
}

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

// ============================================================ US-3.4.2-04 IdentityVerificationData

public sealed record RequestIdentityVerificationCommand(string RequestingComponent, SubjectType SubjectType, Guid SubjectId,
    string NationalIdReference, string FullName, DateOnly DateOfBirth) : ServiceCommand<Guid>;

public sealed class RequestIdentityVerificationValidator : AbstractValidator<RequestIdentityVerificationCommand>
{
    public RequestIdentityVerificationValidator()
    {
        RuleFor(c => c.SubjectId).NotEmpty().WithErrorCode("VAL.SubjectId.Required");
        RuleFor(c => c.NationalIdReference).NotEmpty().MaximumLength(64).WithErrorCode("VAL.NationalIdReference.Required");
        RuleFor(c => c.FullName).NotEmpty().MaximumLength(200).WithErrorCode("VAL.FullName.Required");
        RuleFor(c => c.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow)).WithErrorCode("VAL.DateOfBirth.MustBeInPast");
    }
}

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

// ============================================================ read side (BC-04/BC-05 verification status, handover section 6.1 / Q-02)

public sealed record GetSubjectVerificationStatusQuery(SubjectType SubjectType, Guid SubjectId) : ServiceQuery<SubjectVerificationStatusView>;

internal sealed class GetSubjectVerificationStatusHandler : IQueryHandler<GetSubjectVerificationStatusQuery, SubjectVerificationStatusView>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public GetSubjectVerificationStatusHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<SubjectVerificationStatusView>> Handle(GetSubjectVerificationStatusQuery request, CancellationToken ct) =>
        await _store.GetSubjectVerificationStatusAsync(request.SubjectType, request.SubjectId, ct);
}

// ============================================================ retention (US-2.5-03 AC-03)

public sealed record PurgeExpiredGovernmentDataCommand(int Take = 100) : ServiceCommand<int>;

/// <summary>US-2.5-03 AC-03: retention job calls Purge() on GovernmentVerificationData past RetentionExpiresAtUtc (default 12 months, A-02-012),
/// clearing the encrypted VerifiedFields collection. Known limitation (documented in BC-01.md): EducationalCredentialVerification and
/// IdentityVerificationData carry a single small claim rather than a growable encrypted collection; their own retention purge is not yet wired
/// to a Purge() method, so <see cref="IEducationalCredentialVerificationRepository.ListExpiredAsync"/>/<see cref="IIdentityVerificationRepository.ListExpiredAsync"/>
/// exist for a future pass but are not invoked here.</summary>
internal sealed class PurgeExpiredGovernmentDataHandler : ICommandHandler<PurgeExpiredGovernmentDataCommand, int>
{
    private readonly IGovernmentVerificationDataRepository _govData;
    private readonly TimeProvider _clock;

    public PurgeExpiredGovernmentDataHandler(IGovernmentVerificationDataRepository govData, TimeProvider clock)
    {
        _govData = govData;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(PurgeExpiredGovernmentDataCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var purged = 0;
        foreach (var record in await _govData.ListExpiredAsync(now, request.Take, ct))
        {
            record.Purge();
            purged++;
        }

        return purged;
    }
}
