using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class ListSyncErrorLogHandler : IQueryHandler<ListSyncErrorLogQuery, PagedResult<AuditEntryDto>>
{
    private readonly AuditLogQueryService _auditLogQueryService;

    public ListSyncErrorLogHandler(AuditLogQueryService auditLogQueryService) => _auditLogQueryService = auditLogQueryService;

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListSyncErrorLogQuery q, CancellationToken ct) => _auditLogQueryService.Owned(AuditCategory.SyncError, q, q.IncludeArchived, null, ct);
}
