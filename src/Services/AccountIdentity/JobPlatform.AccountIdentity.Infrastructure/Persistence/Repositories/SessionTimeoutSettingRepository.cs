using JobPlatform.AccountIdentity.Domain.Interfaces.Repositories;
using JobPlatform.AccountIdentity.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Infrastructure.Persistence.Repositories;

internal sealed class SessionTimeoutSettingRepository : ISessionTimeoutSettingRepository
{
    private readonly IdentityDbContext _db;
    private readonly TimeProvider _clock;

    public SessionTimeoutSettingRepository(IdentityDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<SessionTimeoutSetting> GetAsync(CancellationToken ct = default)
    {
        var setting = await _db.SessionSettings.FirstOrDefaultAsync(s => s.Id == SessionTimeoutSetting.SingletonId, ct);
        if (setting is not null)
        {
            return setting;
        }

        setting = SessionTimeoutSetting.CreateDefault(_clock);
        _db.SessionSettings.Add(setting);
        return setting;
    }
}
