using JobPlatform.AccountIdentity.Domain.Sessions;

namespace JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;

public interface ISessionTimeoutSettingRepository
{
    Task<SessionTimeoutSetting> GetAsync(CancellationToken ct = default);
}
