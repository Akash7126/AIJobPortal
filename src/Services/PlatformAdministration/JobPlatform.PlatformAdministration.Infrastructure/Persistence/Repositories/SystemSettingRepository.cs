using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.PlatformAdministration.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.PlatformAdministration.Infrastructure.Persistence.Repositories;

internal sealed class SystemSettingRepository(AdminDbContext db) : ISystemSettingRepository
{
    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);

    public void Add(SystemSetting setting) => db.SystemSettings.Add(setting);
}
