using JobPlatform.Notification.Application.DTOs.InApp;

namespace JobPlatform.Notification.Application.Interfaces;

/// <summary>Pushes a new in-app notification to the recipient's live connections (SignalR). Returns false when the user is offline.</summary>
public interface IRealtimeNotifier
{
    Task<bool> PushAsync(Guid recipient, InAppNotificationDto notification, CancellationToken ct = default);
}
