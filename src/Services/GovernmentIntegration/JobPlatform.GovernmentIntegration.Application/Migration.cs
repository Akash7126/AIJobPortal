using FluentValidation;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.GovernmentIntegration.Application;

// ============================================================ US-6.1-01 MigrationRun.Start

public sealed record StartDataMigrationCommand(IReadOnlyList<string> Phases, bool DryRun) : AdminCommand<MigrationRunView>;

public sealed class StartDataMigrationValidator : AbstractValidator<StartDataMigrationCommand>
{
    public StartDataMigrationValidator() =>
        RuleFor(c => c.Phases).NotEmpty().WithErrorCode("VAL.Phases.Required");
}

internal sealed class StartDataMigrationHandler : ICommandHandler<StartDataMigrationCommand, MigrationRunView>
{
    private readonly IMigrationRunRepository _runs;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public StartDataMigrationHandler(IMigrationRunRepository runs, ICurrentUser user, TimeProvider clock)
    {
        _runs = runs;
        _user = user;
        _clock = clock;
    }

    /// <summary>Single-run guard (handover section 3.7): a Redis lock protects concurrent starts in production; here the repository check
    /// against GetActiveAsync is the same guard expressed at the application level, sufficient for the SQLite/in-memory test topology.</summary>
    public async Task<Result<MigrationRunView>> Handle(StartDataMigrationCommand request, CancellationToken ct)
    {
        if (await _runs.GetActiveAsync(ct) is not null)
        {
            return Error.Conflict(Domain.Common.ErrorCodes.MigrationAlreadyActive, "A migration run is already active.");
        }

        var run = MigrationRun.Start(Guid.NewGuid(), ActorFactory.From(_user), request.Phases, _clock.GetUtcNow().UtcDateTime);
        _runs.Add(run);
        return ToView(run);
    }

    internal static MigrationRunView ToView(MigrationRun r) => new(r.Id, r.InitiatedBy, r.Status.ToString(),
        r.Phases.Select(p => new MigrationPhaseView(p.Name, p.Status.ToString(), p.TestOutcome)).ToArray(),
        r.Log.Select(l => new MigrationLogEntryView(l.Phase, l.Outcome, l.Message, l.AtUtc)).ToArray());
}

// ============================================================ US-6.1-03 phase testing (internal) and rollback

public sealed record TestMigrationPhaseCommand(Guid MigrationRunId, bool Passed, string TestOutcome) : ServiceCommand<MigrationRunView>;

internal sealed class TestMigrationPhaseHandler : ICommandHandler<TestMigrationPhaseCommand, MigrationRunView>
{
    private readonly IMigrationRunRepository _runs;
    private readonly TimeProvider _clock;

    public TestMigrationPhaseHandler(IMigrationRunRepository runs, TimeProvider clock)
    {
        _runs = runs;
        _clock = clock;
    }

    public async Task<Result<MigrationRunView>> Handle(TestMigrationPhaseCommand request, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(request.MigrationRunId, ct);
        if (run is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The migration run was not found.");
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (request.Passed)
        {
            run.AcceptPhase(request.TestOutcome, now);
        }
        else
        {
            run.FailPhase(request.TestOutcome, now);
        }

        return StartDataMigrationHandler.ToView(run);
    }
}

public sealed record RollbackMigrationRunCommand(Guid MigrationRunId, string Reason) : AdminCommand<Unit>;

public sealed class RollbackMigrationRunValidator : AbstractValidator<RollbackMigrationRunCommand>
{
    public RollbackMigrationRunValidator()
    {
        RuleFor(c => c.MigrationRunId).NotEmpty().WithErrorCode("VAL.MigrationRunId.Required");
        RuleFor(c => c.Reason).NotEmpty().WithErrorCode("VAL.Reason.Required");
    }
}

internal sealed class RollbackMigrationRunHandler : ICommandHandler<RollbackMigrationRunCommand, Unit>
{
    private readonly IMigrationRunRepository _runs;
    private readonly ILegacyDataRepository _legacyData;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RollbackMigrationRunHandler(IMigrationRunRepository runs, ILegacyDataRepository legacyData, ICurrentUser user, TimeProvider clock)
    {
        _runs = runs;
        _legacyData = legacyData;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(RollbackMigrationRunCommand request, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(request.MigrationRunId, ct);
        if (run is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The migration run was not found.");
        }

        run.Rollback(ActorFactory.From(_user), _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

public sealed record GetMigrationRunQuery(Guid MigrationRunId) : AdminQuery<MigrationRunView>;

internal sealed class GetMigrationRunHandler : IQueryHandler<GetMigrationRunQuery, MigrationRunView>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public GetMigrationRunHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<MigrationRunView>> Handle(GetMigrationRunQuery request, CancellationToken ct) =>
        await _store.GetMigrationRunAsync(request.MigrationRunId, ct) is { } view
            ? view
            : Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The migration run was not found.");
}

// ============================================================ US-6.1-02 legacy import (internal, background)

public sealed record ImportLegacyDataBatchCommand(Guid MigrationRunId, Guid MigrationBatchId, SourceSystem SourceSystem, int Take)
    : ServiceCommand<int>;

internal sealed class ImportLegacyDataBatchHandler : ICommandHandler<ImportLegacyDataBatchCommand, int>
{
    private readonly ILegacyDataRepository _repository;
    private readonly ILegacySourceReader _reader;
    private readonly TimeProvider _clock;

    public ImportLegacyDataBatchHandler(ILegacyDataRepository repository, ILegacySourceReader reader, TimeProvider clock)
    {
        _repository = repository;
        _reader = reader;
        _clock = clock;
    }

    /// <summary>INV-11 idempotency: a record already known for (SourceSystem, SourceRecordId) is left unchanged (AC-04). Mapping/transformation
    /// are identity stubs (handover Q-04: the real MoL/PEF schema is unknown) so the pipeline's stage machine and validation are exercised
    /// end-to-end without depending on an unavailable schema.</summary>
    public async Task<Result<int>> Handle(ImportLegacyDataBatchCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var imported = 0;
        foreach (var record in await _reader.ReadBatchAsync(request.SourceSystem, request.Take, ct))
        {
            if (await _repository.GetBySourceKeyAsync(request.SourceSystem, record.SourceRecordId, ct) is not null)
            {
                continue;
            }

            var legacy = LegacyData.Import(Guid.NewGuid(), request.MigrationRunId, request.MigrationBatchId, request.SourceSystem,
                record.SourceRecordId, record.RecordType, record.Payload);
            legacy.MapToNewSchema(record.Payload);
            legacy.Transform(record.Payload);
            var errors = string.IsNullOrWhiteSpace(record.Payload) ? new[] { Domain.Common.ErrorCodes.LegacyInvalidField } : Array.Empty<string>();
            legacy.Validate(errors);
            if (legacy.Stage == LegacyStage.Validated)
            {
                legacy.MarkMigrated(now);
                imported++;
            }

            _repository.Add(legacy);
        }

        return imported;
    }
}

// ============================================================ US-6.1-04 DataQuality

public sealed record CleanseMigratedDataCommand(
    Guid MigrationRunId, Guid BatchId, string SnapshotVersion, int IssuesResolved, int DuplicatesRemoved, int FormatsStandardized,
    int RecordsChecked, int RecordsRejected) : ServiceCommand<DataQualityView>;

internal sealed class CleanseMigratedDataHandler : ICommandHandler<CleanseMigratedDataCommand, DataQualityView>
{
    private readonly IDataQualityRepository _repository;
    private readonly TimeProvider _clock;

    public CleanseMigratedDataHandler(IDataQualityRepository repository, TimeProvider clock)
    {
        _repository = repository;
        _clock = clock;
    }

    /// <summary>INV-13 snapshot isolation: a batch already running a cleansing pass refuses a second concurrent one (AC-03; the handover's
    /// "queued, processed after Complete" is realised here as an immediate 409 - the caller retries once the running pass completes).</summary>
    public async Task<Result<DataQualityView>> Handle(CleanseMigratedDataCommand request, CancellationToken ct)
    {
        if (await _repository.HasRunningForBatchAsync(request.BatchId, ct))
        {
            return Error.Conflict(Domain.Common.ErrorCodes.DataQualitySnapshotIsolation, "A cleansing run is already active for this batch.");
        }

        var dataQuality = DataQuality.Start(Guid.NewGuid(), request.MigrationRunId, request.BatchId, request.SnapshotVersion);
        _repository.Add(dataQuality);
        dataQuality.Record(request.IssuesResolved, request.DuplicatesRemoved, request.FormatsStandardized, request.RecordsChecked, request.RecordsRejected);
        dataQuality.Complete(_clock.GetUtcNow().UtcDateTime);
        return ToView(dataQuality);
    }

    internal static DataQualityView ToView(DataQuality d) => new(d.Id, d.MigrationRunId, d.BatchId, d.Status.ToString(), d.IssuesResolved,
        d.DuplicatesRemoved, d.FormatsStandardized, d.RecordsChecked, d.RecordsRejected);
}

public sealed record GetDataQualityQuery(Guid BatchId) : AdminQuery<DataQualityView>;

internal sealed class GetDataQualityHandler : IQueryHandler<GetDataQualityQuery, DataQualityView>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public GetDataQualityHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<DataQualityView>> Handle(GetDataQualityQuery request, CancellationToken ct) =>
        await _store.GetDataQualityByBatchAsync(request.BatchId, ct) is { } view
            ? view
            : Error.NotFound(Domain.Common.ErrorCodes.NotFound, "No data-quality run was found for this batch.");
}

// ============================================================ BC-07 backfill (US-3.4.2-05, handover section 6.1)

public sealed record ListGovernmentExchangesQuery(DateTime? From, DateTime? To, int Page = 1, int PageSize = 50)
    : ServiceQuery<SharedKernel.Application.Paging.PagedResult<GovernmentAccessLogView>>;

internal sealed class ListGovernmentExchangesHandler :
    IQueryHandler<ListGovernmentExchangesQuery, SharedKernel.Application.Paging.PagedResult<GovernmentAccessLogView>>
{
    private readonly IGovernmentIntegrationReadStore _store;

    public ListGovernmentExchangesHandler(IGovernmentIntegrationReadStore store) => _store = store;

    public async Task<Result<SharedKernel.Application.Paging.PagedResult<GovernmentAccessLogView>>> Handle(ListGovernmentExchangesQuery request,
        CancellationToken ct) =>
        await _store.ListGovernmentExchangesAsync(request.From, request.To, new SharedKernel.Application.Paging.PageRequest(request.Page, request.PageSize), ct);
}
