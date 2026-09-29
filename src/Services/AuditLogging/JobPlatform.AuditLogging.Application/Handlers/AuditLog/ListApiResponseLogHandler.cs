using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class ListApiResponseLogHandler : IQueryHandler<ListApiResponseLogQuery, PagedResult<AuditEntryDto>>
{
    private readonly AuditLogQueryService _auditLogQueryService;

    public ListApiResponseLogHandler(AuditLogQueryService auditLogQueryService) => _auditLogQueryService = auditLogQueryService;

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListApiResponseLogQuery q, CancellationToken ct) => _auditLogQueryService.Owned(AuditCategory.ApiCall, q, q.IncludeArchived, null, ct);
}
