using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.Notification.Application.DTOs.InApp;

public sealed record InAppPage(PagedResult<InAppNotificationDto> Items, int Unread);
