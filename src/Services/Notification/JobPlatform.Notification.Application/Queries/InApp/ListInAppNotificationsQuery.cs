using JobPlatform.Notification.Application.DTOs.InApp;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Queries.InApp;

public sealed record ListInAppNotificationsQuery(string? Status, int Page = 1, int PageSize = 20)
    : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), IQuery<InAppPage>;
