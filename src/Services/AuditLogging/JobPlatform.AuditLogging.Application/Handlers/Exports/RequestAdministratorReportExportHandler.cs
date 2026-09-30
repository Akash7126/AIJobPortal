using JobPlatform.AuditLogging.Application.Commands.Exports;
using JobPlatform.AuditLogging.Application.DTOs.Exports;
using JobPlatform.AuditLogging.Application.Exports;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.Exports;

internal sealed class RequestAdministratorReportExportHandler : ICommandHandler<RequestAdministratorReportExportCommand, ExportRequestResult>
{
    private readonly IExportJobRepository _jobs;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public RequestAdministratorReportExportHandler(IExportJobRepository jobs, ICurrentUser user, TimeProvider clock)
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
}
