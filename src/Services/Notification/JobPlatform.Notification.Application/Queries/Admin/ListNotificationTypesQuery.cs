using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Queries.Admin;

public sealed record ListNotificationTypesQuery : AdminRequest, IQuery<IReadOnlyList<NotificationTypeDto>>
{
    public ListNotificationTypesQuery() : base(NotificationErrorCodes.TypeForbidden)
    {
    }
}
