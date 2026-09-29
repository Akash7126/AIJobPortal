using JobPlatform.AuditLogging.Application.Commands.Exports;
using JobPlatform.AuditLogging.Application.DTOs.Exports;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.Exports;

internal sealed class StartExportGenerationHandler : ICommandHandler<StartExportGenerationCommand, ExportJobSpec>
{
    private readonly IExportJobRepository _jobs;

    public StartExportGenerationHandler(IExportJobRepository jobs) => _jobs = jobs;

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
}
