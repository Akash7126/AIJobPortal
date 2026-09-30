using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence.Repositories;

internal sealed class NotificationTypeRepository : INotificationTypeRepository
{
    private readonly NotificationDbContext _db;

    public NotificationTypeRepository(NotificationDbContext db) => _db = db;

    public Task<NotificationType?> GetAsync(string code, CancellationToken ct = default) => _db.NotificationTypes.FirstOrDefaultAsync(t => t.Id == code, ct);

    public async Task<IReadOnlyList<NotificationType>> ListAsync(CancellationToken ct = default) =>
        await _db.NotificationTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);

    public void Add(NotificationType type) => _db.NotificationTypes.Add(type);
}
