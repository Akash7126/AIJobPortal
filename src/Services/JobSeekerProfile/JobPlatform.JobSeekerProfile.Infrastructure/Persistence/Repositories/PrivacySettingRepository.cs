using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;

internal sealed class PrivacySettingRepository(JobSeekerProfileDbContext db) : IPrivacySettingRepository
{
    public Task<PrivacySetting?> GetByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.PrivacySettings.FirstOrDefaultAsync(p => p.ProfileId == profileId, ct);

    public void Add(PrivacySetting setting) => db.PrivacySettings.Add(setting);
}
