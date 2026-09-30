using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.Notification.Application.DTOs.InApp;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.Notification.Application.Interfaces;

public interface INotificationReadStore
{
    Task<PagedResult<InAppNotificationDto>> ListInAppAsync(Guid recipient, InAppStatus? status, PageRequest page, CancellationToken ct = default);

    Task<int> CountUnreadAsync(Guid recipient, CancellationToken ct = default);

    Task<PagedResult<SmsDeliveryDto>> ListSmsDeliveriesAsync(DeliveryStatus? status, PageRequest page, CancellationToken ct = default);

    Task<NotificationDetailDto?> GetDetailAsync(Guid id, CancellationToken ct = default);
}
