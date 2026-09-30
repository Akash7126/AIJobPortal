using JobPlatform.PlatformAdministration.Domain.Settings;

namespace JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;

public interface ISystemSettingRepository
{
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default);

    void Add(SystemSetting setting);
}
