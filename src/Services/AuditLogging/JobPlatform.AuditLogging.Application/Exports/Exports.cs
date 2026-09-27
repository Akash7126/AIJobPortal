using FluentValidation;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Logging;
using Unit = JobPlatform.SharedKernel.Application.Results.Unit;

namespace JobPlatform.AuditLogging.Application.Exports;

// ---------------------------------------------------------------------- administrator report export (US-3.1.4-10)

/// <param name="ReportType">One of the ReportType names (PostingsBySector, ...).</param>
/// <param name="Format">Csv, Json or Xml (THR-081).</param>
public sealed record RequestAdministratorReportExportCommand(string ReportType, string Format, IReadOnlyDictionary<string, string>? Parameters)
    : AdminRequest(AuditErrorCodes.AdminForbidden), ICommand<ExportRequestResult>, IConflictAwareCommand
{
    public string UniqueViolationErrorCode => "E-AUDIT-EXPORT-DUPLICATE";
}

/// <summary>Reused = an identical request was already queued or generating and that job is returned instead of starting another (AC-03).</summary>
public sealed record ExportRequestResult(ExportJobDto Job, bool Reused);

public sealed record GetExportJobQuery(Guid Id) : AdminRequest(AuditErrorCodes.AdminForbidden), IQuery<ExportJobDto>;

public sealed class RequestAdministratorReportExportValidator : AbstractValidator<RequestAdministratorReportExportCommand>
{
    public RequestAdministratorReportExportValidator()
    {
        RuleFor(x => x.ReportType).NotEmpty().WithErrorCode("VAL.ReportType.Required").DependentRules(() =>
            RuleFor(x => x.ReportType).Must(t => Enum.TryParse<ReportType>(t, true, out _)).WithErrorCode("VAL.ReportType.Invalid"));
        RuleFor(x => x.Format).NotEmpty().WithErrorCode("VAL.Format.Required").DependentRules(() =>
            RuleFor(x => x.Format).Must(f => Enum.TryParse<ExportFormat>(f, true, out _)).WithErrorCode("VAL.Format.Invalid"));
        When(x => x.Parameters is not null, () =>
            RuleFor(x => x.Parameters!).Must(p => p.Count <= 20 && p.All(kv => kv.Key.Length <= 50 && kv.Value.Length <= 200)).WithErrorCode("VAL.Parameters.Invalid"));
    }
}

public sealed class GetExportJobQueryValidator : AbstractValidator<GetExportJobQuery>
{
    public GetExportJobQueryValidator() => RuleFor(x => x.Id).NotEmpty().WithErrorCode("VAL.Id.Required");
}

internal static class ExportMapping
{
    public static ExportJobDto ToDto(ExportJob job) => new(job.Id, job.ReportType.ToString(), job.Format.ToString(), job.Status.ToString(), job.ResultRef,
        job.FailureReason, job.RequestedAtUtc, job.CompletedAtUtc);
}

internal sealed class ExportHandlers :
    ICommandHandler<RequestAdministratorReportExportCommand, ExportRequestResult>,
    IQueryHandler<GetExportJobQuery, ExportJobDto>,
    ICommandHandler<StartExportGenerationCommand, ExportJobSpec>,
    ICommandHandler<CompleteExportJobCommand, Unit>,
    ICommandHandler<FailExportJobCommand, Unit>
{
    private readonly IExportJobRepository _jobs;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public ExportHandlers(IExportJobRepository jobs, ICurrentUser user, TimeProvider clock)
    {
        _jobs = jobs;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<ExportRequestResult>> Handle(RequestAdministratorReportExportCommand request, CancellationToken ct)
    {
        var type = Enum.Parse<ReportType>(request.ReportType, true);
        var format = Enum.Parse<ExportFormat>(request.Format, true);
        var parameters = request.Parameters ?? new Dictionary<string, string>();
        var administrator = _user.UserId!.Value;

        // INV-04: an identical request while one is Queued or Generating reuses it.
        var hash = ExportJob.HashParameters(type, format, parameters);
        var existing = await _jobs.FindInProgressAsync(administrator, type, hash, ct);
        if (existing is not null)
        {
            return new ExportRequestResult(ExportMapping.ToDto(existing), true);
        }

        var job = ExportJob.Request(administrator, type, format, parameters, _clock.GetUtcNow().UtcDateTime);
        _jobs.Add(job);
        return new ExportRequestResult(ExportMapping.ToDto(job), false);
    }

    public async Task<Result<ExportJobDto>> Handle(GetExportJobQuery request, CancellationToken ct)
    {
        var job = await _jobs.GetAsync(request.Id, ct);
        return job is null
            ? Error.NotFound(AuditErrorCodes.ExportNotFound, "The export job was not found.")
            : ExportMapping.ToDto(job);
    }

    public async Task<Result<ExportJobSpec>> Handle(StartExportGenerationCommand request, CancellationToken ct)
    {
        var job = await _jobs.GetAsync(request.JobId, ct);
        if (job is null)
        {
            return Error.NotFound(AuditErrorCodes.ExportNotFound, "The export job was not found.");
        }

        job.StartGenerating();
        return new ExportJobSpec(job.Id, job.ReportType, job.Format,
            System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(job.ParametersJson) ?? new Dictionary<string, string>());
    }

    public async Task<Result<Unit>> Handle(CompleteExportJobCommand request, CancellationToken ct)
    {
        var job = await _jobs.GetAsync(request.JobId, ct);
        if (job is null)
        {
            return Error.NotFound(AuditErrorCodes.ExportNotFound, "The export job was not found.");
        }

        job.Complete(request.ResultRef, _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }

    public async Task<Result<Unit>> Handle(FailExportJobCommand request, CancellationToken ct)
    {
        var job = await _jobs.GetAsync(request.JobId, ct);
        if (job is null)
        {
            return Error.NotFound(AuditErrorCodes.ExportNotFound, "The export job was not found.");
        }

        job.Fail(request.Reason, _clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------- background generation (no DB transaction held across the report call)

public sealed record ExportJobSpec(Guid JobId, ReportType ReportType, ExportFormat Format, IReadOnlyDictionary<string, string> Parameters);

public sealed record StartExportGenerationCommand(Guid JobId) : ICommand<ExportJobSpec>;

public sealed record CompleteExportJobCommand(Guid JobId, string ResultRef) : ICommand;

public sealed record FailExportJobCommand(Guid JobId, string Reason) : ICommand;

/// <summary>
/// Drives queued exports to Ready or Failed: mark generating (committed), call the report source outside any transaction, then record the outcome.
/// A source failure or an exception marks the job Failed; it never blocks the next job.
/// </summary>
public sealed class ExportGenerationRunner
{
    private readonly ISender _sender;
    private readonly IExportJobRepository _jobs;
    private readonly IReportGenerator _generator;
    private readonly ILogger<ExportGenerationRunner> _logger;

    public ExportGenerationRunner(ISender sender, IExportJobRepository jobs, IReportGenerator generator, ILogger<ExportGenerationRunner> logger)
    {
        _sender = sender;
        _jobs = jobs;
        _generator = generator;
        _logger = logger;
    }

    /// <summary>Processes up to <paramref name="take"/> queued jobs; returns how many were handled.</summary>
    public async Task<int> RunOnceAsync(int take, CancellationToken ct)
    {
        var queued = await _jobs.ListQueuedAsync(take, ct);
        foreach (var queuedJob in queued)
        {
            var started = await _sender.Send(new StartExportGenerationCommand(queuedJob.Id), ct);
            if (started.IsFailure)
            {
                continue;
            }

            try
            {
                var report = await _generator.GenerateAsync(started.Value.ReportType, started.Value.Format, started.Value.Parameters, ct);
                if (report.IsSuccess)
                {
                    await _sender.Send(new CompleteExportJobCommand(queuedJob.Id, report.Value), ct);
                }
                else
                {
                    await _sender.Send(new FailExportJobCommand(queuedJob.Id, report.Error!.Code), ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Export job {JobId} failed", queuedJob.Id);
                await _sender.Send(new FailExportJobCommand(queuedJob.Id, "E-AUDIT-REPORT-SOURCE-UNAVAILABLE"), ct);
            }
        }

        return queued.Count;
    }
}

// ---------------------------------------------------------------------- retention (AL.Retention.12_MONTHS)

/// <summary>Archives entries whose retain-until instant has passed (kept, not purged). Scheduled by the infrastructure; a system command with no caller.</summary>
public sealed record ArchiveExpiredAuditEntriesCommand(int BatchSize) : ICommand<int>;

internal sealed class ArchiveHandler : ICommandHandler<ArchiveExpiredAuditEntriesCommand, int>
{
    private readonly IAuditEntryRepository _entries;
    private readonly RetentionPolicy _retention;
    private readonly TimeProvider _clock;

    public ArchiveHandler(IAuditEntryRepository entries, RetentionPolicy retention, TimeProvider clock)
    {
        _entries = entries;
        _retention = retention;
        _clock = clock;
    }

    public async Task<Result<int>> Handle(ArchiveExpiredAuditEntriesCommand request, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expired = await _entries.ListExpiredAsync(now, request.BatchSize, ct);
        var archived = 0;
        foreach (var entry in expired.Where(e => _retention.IsExpired(e.RetainUntilUtc, now)))
        {
            entry.Archive(now);
            archived++;
        }

        return archived;
    }
}
