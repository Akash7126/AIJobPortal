using JobPlatform.AuditLogging.Application.DTOs.Exports;
using JobPlatform.AuditLogging.Application.Exports;
using JobPlatform.AuditLogging.Application.Queries.Exports;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.Exports;

internal sealed class GetExportJobHandler : IQueryHandler<GetExportJobQuery, ExportJobDto>
{
    private readonly IExportJobRepository _jobs;

    public GetExportJobHandler(IExportJobRepository jobs) => _jobs = jobs;

    public async Task<Result<ExportJobDto>> Handle(GetExportJobQuery request, CancellationToken ct)
    {
        var job = await _jobs.GetAsync(request.Id, ct);
        return job is null
            ? Error.NotFound(AuditErrorCodes.ExportNotFound, "The export job was not found.")
            : ExportMapping.ToDto(job);
    }
}
