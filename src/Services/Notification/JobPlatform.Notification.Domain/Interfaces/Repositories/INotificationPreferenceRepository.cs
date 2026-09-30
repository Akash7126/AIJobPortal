namespace JobPlatform.Notification.Domain.Interfaces.Repositories;

public interface INotificationPreferenceRepository
{
    /// <summary>The stored preferences, or null when the user never saved any (callers then use <see cref="NotificationPreference.Default"/>).</summary>
    Task<NotificationPreference?> GetAsync(Guid accountId, CancellationToken ct = default);

    void Add(NotificationPreference preference);
}
