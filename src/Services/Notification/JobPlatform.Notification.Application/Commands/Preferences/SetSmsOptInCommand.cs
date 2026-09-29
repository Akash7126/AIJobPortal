using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Commands.Preferences;

public sealed record SetSmsOptInCommand(string? Mobile, bool OptIn) : AuthenticatedRequest(NotificationErrorCodes.SmsForbidden), ICommand<NotificationPreferencesDto>;
