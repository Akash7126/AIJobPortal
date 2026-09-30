namespace JobPlatform.AuditLogging.Domain.Interfaces.Repositories;

public interface INotificationLogRepository
{
    Task<NotificationLogEntry?> GetAsync(Guid notificationId, CancellationToken ct = default);

    void Add(NotificationLogEntry entry);
}
