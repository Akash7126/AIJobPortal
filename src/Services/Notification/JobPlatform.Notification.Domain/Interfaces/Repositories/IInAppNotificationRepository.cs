namespace JobPlatform.Notification.Domain.Interfaces.Repositories;

public interface IInAppNotificationRepository
{
    Task<InAppNotification?> GetAsync(Guid id, CancellationToken ct = default);

    void Add(InAppNotification notification);
}
