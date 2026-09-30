namespace JobPlatform.Notification.Domain.Interfaces.Repositories;

public interface INotificationTypeRepository
{
    Task<NotificationType?> GetAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<NotificationType>> ListAsync(CancellationToken ct = default);

    void Add(NotificationType type);
}
