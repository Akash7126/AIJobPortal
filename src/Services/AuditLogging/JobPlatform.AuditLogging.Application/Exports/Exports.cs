using JobPlatform.AuditLogging.Application.Commands.Exports;
using JobPlatform.AuditLogging.Application.DTOs.Exports;
using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AuditLogging.Application.Exports;

internal static class ExportMapping
{
    public static ExportJobDto ToDto(ExportJob job) => new(job.Id, job.ReportType.ToString(), job.Format.ToString(), job.Status.ToString(), job.ResultRef,
        job.FailureReason, job.RequestedAtUtc, job.CompletedAtUtc);
}

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
