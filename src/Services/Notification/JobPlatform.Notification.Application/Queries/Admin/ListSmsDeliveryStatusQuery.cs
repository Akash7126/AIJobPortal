using JobPlatform.Notification.Application.DTOs.Admin;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.Notification.Application.Queries.Admin;

public sealed record ListSmsDeliveryStatusQuery(string? Status, int Page = 1, int PageSize = 20)
    : AdminRequest(NotificationErrorCodes.SmsForbidden), IQuery<PagedResult<SmsDeliveryDto>>;
