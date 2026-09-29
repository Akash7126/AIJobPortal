using JobPlatform.Notification.Application.DTOs.InApp;
using JobPlatform.Notification.Application.Queries.InApp;
using JobPlatform.Notification.Application.Services.InApp;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.InApp;

internal sealed class ListInAppNotificationsHandler : IQueryHandler<ListInAppNotificationsQuery, InAppPage>
{
    private readonly INotificationReadStore _store;
    private readonly InAppService _inAppService;

    public ListInAppNotificationsHandler(INotificationReadStore store, InAppService inAppService)
    {
        _store = store;
        _inAppService = inAppService;
    }

    public async Task<Result<InAppPage>> Handle(ListInAppNotificationsQuery request, CancellationToken ct)
    {
        // Only the caller's own notifications are ever read (INV-01 recipient-scoped).
        var status = string.IsNullOrEmpty(request.Status) ? (InAppStatus?)null : Enum.Parse<InAppStatus>(request.Status, true);
        var page = await _store.ListInAppAsync(_inAppService.Me.Id, status, new PageRequest(request.Page, request.PageSize), ct);
        return new InAppPage(page, await _store.CountUnreadAsync(_inAppService.Me.Id, ct));
    }
}
