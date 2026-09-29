using JobPlatform.Notification.Application.DTOs.InApp;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.InApp;

public sealed record TakeNotificationActionCommand(Guid Id) : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), ICommand<NotificationActionDto>;
