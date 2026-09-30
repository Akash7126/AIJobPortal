using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;

internal sealed class NotificationLogRepository : INotificationLogRepository
{
    private readonly AuditDbContext _db;

    public NotificationLogRepository(AuditDbContext db) => _db = db;

    public Task<NotificationLogEntry?> GetAsync(Guid notificationId, CancellationToken ct = default) =>
        _db.NotificationLog.FirstOrDefaultAsync(n => n.Id == notificationId, ct);

    public void Add(NotificationLogEntry entry) => _db.NotificationLog.Add(entry);
}
