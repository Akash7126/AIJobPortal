using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.Notification.Infrastructure.Persistence.Repositories;

internal sealed class WeeklyCycleRepository : IWeeklyCycleRepository
{
    private readonly NotificationDbContext _db;

    public WeeklyCycleRepository(NotificationDbContext db) => _db = db;

    public async Task<bool> ExistsAsync(Guid accountId, string isoWeek, CancellationToken ct = default) =>
        _db.WeeklyCycles.Local.Any(c => c.AccountId == accountId && c.IsoWeek == isoWeek) || await _db.WeeklyCycles.AnyAsync(c => c.AccountId == accountId && c.IsoWeek == isoWeek, ct);

    public void Add(WeeklyCycle cycle) => _db.WeeklyCycles.Add(cycle);
}
