using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence.Repositories;

/// <summary>Aggregate-oriented repositories: no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
internal sealed class InAppNotificationRepository : IInAppNotificationRepository
{
    private readonly NotificationDbContext _db;

    public InAppNotificationRepository(NotificationDbContext db) => _db = db;

    public Task<InAppNotification?> GetAsync(Guid id, CancellationToken ct = default) => _db.InAppNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public void Add(InAppNotification notification) => _db.InAppNotifications.Add(notification);
}
