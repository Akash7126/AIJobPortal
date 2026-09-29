using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class GetJobAuditTrailHandler : IQueryHandler<GetJobAuditTrailQuery, PagedResult<AuditEntryDto>>
{
    private readonly AuditLogQueryService _auditLogQueryService;

    public GetJobAuditTrailHandler(AuditLogQueryService auditLogQueryService) => _auditLogQueryService = auditLogQueryService;

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(GetJobAuditTrailQuery q, CancellationToken ct) =>
        _auditLogQueryService.Owned(AuditCategory.JobAudit, q, q.IncludeArchived, q.PlatformJobId, ct);
}
