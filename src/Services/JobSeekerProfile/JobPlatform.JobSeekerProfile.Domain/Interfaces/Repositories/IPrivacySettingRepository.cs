namespace JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;

public interface IPrivacySettingRepository
{
    Task<PrivacySetting?> GetByProfileAsync(Guid profileId, CancellationToken ct = default);
    void Add(PrivacySetting setting);
}
