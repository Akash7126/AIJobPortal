using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class ListSmsMessageLogHandler : IQueryHandler<ListSmsMessageLogQuery, PagedResult<NotificationLogDto>>
{
    private readonly IAuditReadStore _store;
    private readonly NotificationLogQueryService _notificationLogQueryService;

    public ListSmsMessageLogHandler(IAuditReadStore store, NotificationLogQueryService notificationLogQueryService)
    {
        _store = store;
        _notificationLogQueryService = notificationLogQueryService;
    }

    public async Task<Result<PagedResult<NotificationLogDto>>> Handle(ListSmsMessageLogQuery q, CancellationToken ct)
    {
        AccessScopePolicy.EnsureCanView(AuditCategory.Sms, _notificationLogQueryService.Viewer(), OwnerScope.AdminOnly);
        return await _store.ListNotificationLogAsync(new[] { "Sms" }, null, new PageRequest(q.Page, q.PageSize), ct);
    }
}
