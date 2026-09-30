using JobPlatform.AuditLogging.Application.DTOs.AuditLog;
using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Application.Queries.AuditLog;
using JobPlatform.AuditLogging.Application.Services.AuditLog;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AuditLogging.Application.Handlers.AuditLog;

internal sealed class ListNotificationHistoryHandler : IQueryHandler<ListNotificationHistoryQuery, PagedResult<NotificationLogDto>>
{
    private readonly IAuditReadStore _store;
    private readonly NotificationLogQueryService _notificationLogQueryService;

    public ListNotificationHistoryHandler(IAuditReadStore store, NotificationLogQueryService notificationLogQueryService)
    {
        _store = store;
        _notificationLogQueryService = notificationLogQueryService;
    }

    public async Task<Result<PagedResult<NotificationLogDto>>> Handle(ListNotificationHistoryQuery q, CancellationToken ct)
    {
        var viewer = _notificationLogQueryService.Viewer();
        AccessScopePolicy.EnsureCanView(AuditCategory.Notification, viewer, OwnerScope.Of(OwnerType.User, viewer.Id ?? Guid.Empty));
        return await _store.ListNotificationLogAsync(new[] { "InApp", "Email", "Sms" }, viewer.Id, new PageRequest(q.Page, q.PageSize), ct);
    }
}
