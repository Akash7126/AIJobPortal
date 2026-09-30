using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.Notification.Application.Interfaces;
using JobPlatform.Notification.Application.Queries.Delivery;
using JobPlatform.Notification.Application.Services.Delivery;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Delivery;

internal sealed class GetNotificationDetailHandler : IQueryHandler<GetNotificationDetailQuery, NotificationDetailDto>
{
    private readonly INotificationReadStore _store;

    public GetNotificationDetailHandler(INotificationReadStore store) => _store = store;

    public async Task<Result<NotificationDetailDto>> Handle(GetNotificationDetailQuery request, CancellationToken ct) =>
        await _store.GetDetailAsync(request.Id, ct) is { } detail ? detail : DeliveryService.NotFound;
}
