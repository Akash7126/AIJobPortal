using JobPlatform.Notification.Application.DTOs.Preferences;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;

namespace JobPlatform.Notification.Application.Queries.Preferences;

/// <summary>channel is email, in-app or sms.</summary>
public sealed record GetNotificationPreferencesQuery(string Channel) : AuthenticatedRequest(NotificationErrorCodes.InAppForbidden), IQuery<NotificationPreferencesDto>;
