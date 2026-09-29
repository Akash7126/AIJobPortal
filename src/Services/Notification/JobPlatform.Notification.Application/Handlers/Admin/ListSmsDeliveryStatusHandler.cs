using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Application.Queries.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Admin;

internal sealed class ListSmsDeliveryStatusHandler : IQueryHandler<ListSmsDeliveryStatusQuery, PagedResult<SmsDeliveryDto>>
{
    private readonly INotificationReadStore _store;

    public ListSmsDeliveryStatusHandler(INotificationReadStore store) => _store = store;

    public async Task<Result<PagedResult<SmsDeliveryDto>>> Handle(ListSmsDeliveryStatusQuery request, CancellationToken ct) =>
        await _store.ListSmsDeliveriesAsync(string.IsNullOrEmpty(request.Status) ? null : Enum.Parse<DeliveryStatus>(request.Status, true),
            new PageRequest(request.Page, request.PageSize), ct);
}
