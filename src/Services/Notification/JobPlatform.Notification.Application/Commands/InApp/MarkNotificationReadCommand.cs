using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Commands.InApp;

public sealed record MarkNotificationReadCommand(Guid Id) : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), ICommand;
