using JobPlatform.AuditLogging.Application.Commands.Exports;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Unit = JobPlatform.SharedKernel.Application.Results.Unit;

namespace JobPlatform.AuditLogging.Application.Handlers.Exports;

internal sealed class CompleteExportJobHandler : ICommandHandler<CompleteExportJobCommand, Unit>
{
    private readonly IExportJobRepository _jobs;
    private readonly TimeProvider _clock;

    public CompleteExportJobHandler(IExportJobRepository jobs, TimeProvider clock)
    {
        _jobs = jobs;
        _clock = clock;
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
}
