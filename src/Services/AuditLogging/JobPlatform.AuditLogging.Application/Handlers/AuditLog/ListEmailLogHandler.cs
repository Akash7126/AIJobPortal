using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class ListEmailLogHandler : IQueryHandler<ListEmailLogQuery, PagedResult<NotificationLogDto>>
{
    private readonly IAuditReadStore _store;
    private readonly NotificationLogQueryService _notificationLogQueryService;

    public ListEmailLogHandler(IAuditReadStore store, NotificationLogQueryService notificationLogQueryService)
    {
        _store = store;
        _notificationLogQueryService = notificationLogQueryService;
    }

    public async Task<Result<PagedResult<NotificationLogDto>>> Handle(ListEmailLogQuery q, CancellationToken ct)
    {
        AccessScopePolicy.EnsureCanView(AuditCategory.Email, _notificationLogQueryService.Viewer(), OwnerScope.AdminOnly);
        return await _store.ListNotificationLogAsync(new[] { "Email" }, null, new PageRequest(q.Page, q.PageSize), ct);
    }
}
