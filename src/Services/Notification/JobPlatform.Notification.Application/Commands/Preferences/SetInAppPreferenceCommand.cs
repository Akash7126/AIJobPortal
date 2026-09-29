using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.Preferences;

public sealed record SetInAppPreferenceCommand(IReadOnlyDictionary<string, bool> Categories)
    : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), ICommand<NotificationPreferencesDto>;
