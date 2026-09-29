using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Application.Queries.Admin;
using JobPlatform.Notification.Application.Services.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.Notification.Application.Handlers.Admin;

internal sealed class ListNotificationTypesHandler : IQueryHandler<ListNotificationTypesQuery, IReadOnlyList<NotificationTypeDto>>
{
    private readonly INotificationTypeRepository _types;

    public ListNotificationTypesHandler(INotificationTypeRepository types) => _types = types;

    public async Task<Result<IReadOnlyList<NotificationTypeDto>>> Handle(ListNotificationTypesQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<NotificationTypeDto>>((await _types.ListAsync(ct)).Select(AdminService.ToDto).ToList());
}
