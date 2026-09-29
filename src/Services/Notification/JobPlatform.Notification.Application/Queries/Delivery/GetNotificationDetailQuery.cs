using JobPlatform.Notification.Application.DTOs.Delivery;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Queries.Delivery;

public sealed record GetNotificationDetailQuery(Guid Id) : ServiceRequest, IQuery<NotificationDetailDto>;
