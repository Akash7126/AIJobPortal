using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence.Repositories;

internal sealed class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly NotificationDbContext _db;

    public NotificationPreferenceRepository(NotificationDbContext db) => _db = db;

    public async Task<NotificationPreference?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        _db.NotificationPreferences.Local.FirstOrDefault(p => p.Id == accountId) ?? await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.Id == accountId, ct);

    public void Add(NotificationPreference preference) => _db.NotificationPreferences.Add(preference);
}
