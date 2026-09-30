using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.Notification.Application.Commands.Preferences;

public sealed record SetEmailPreferenceCommand(IReadOnlyDictionary<string, bool> Categories, string Mode)
    : AuthenticatedRequest(NotificationErrorCodes.EmailForbidden), ICommand<NotificationPreferencesDto>;
