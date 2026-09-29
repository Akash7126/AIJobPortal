using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class ListGovernmentDataAuditTrailHandler : IQueryHandler<ListGovernmentDataAuditTrailQuery, PagedResult<AuditEntryDto>>
{
    private readonly AuditLogQueryService _auditLogQueryService;

    public ListGovernmentDataAuditTrailHandler(AuditLogQueryService auditLogQueryService) => _auditLogQueryService = auditLogQueryService;

    public Task<Result<PagedResult<AuditEntryDto>>> Handle(ListGovernmentDataAuditTrailQuery q, CancellationToken ct) =>
        _auditLogQueryService.Owned(AuditCategory.GovernmentExchange, q, q.IncludeArchived, null, ct);
}
