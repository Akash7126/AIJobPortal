using JobPlatform.JobSeekerProfile.Domain.Common;

namespace JobPlatform.JobSeekerProfile.Domain;

public interface IProfileRepository
{
    Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Profile?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);
    Task<bool> ExistsByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default);
    void Add(Profile profile);
}

public interface IResumeRepository
{
    Task<Resume?> GetCurrentByProfileAsync(Guid profileId, CancellationToken ct = default);
    Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Resume resume);
}

public interface IProfileShareLinkRepository
{
    Task<ProfileShareLink?> GetActiveByProfileAsync(Guid profileId, CancellationToken ct = default);
    Task<ProfileShareLink?> GetByTokenAsync(string token, CancellationToken ct = default);
    void Add(ProfileShareLink link);
}

public interface ISupplementaryDocumentRepository
{
    Task<SupplementaryDocument?> GetByHashAsync(DocumentOwnerType ownerType, Guid ownerId, string sha256, CancellationToken ct = default);
    Task<IReadOnlyList<SupplementaryDocument>> ListByOwnerAsync(DocumentOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task<SupplementaryDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(SupplementaryDocument document);
    void Remove(SupplementaryDocument document);
}

public interface IJobPreferenceRepository
{
    Task<JobPreference?> GetByProfileAsync(Guid profileId, CancellationToken ct = default);
    void Add(JobPreference preference);
}

public interface IPrivacySettingRepository
{
    Task<PrivacySetting?> GetByProfileAsync(Guid profileId, CancellationToken ct = default);
    void Add(PrivacySetting setting);
}

public interface IKnownAccountRepository
{
    Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default);
    void Add(KnownAccount account);
}

public interface IProcessedParsedDataRepository
{
    Task<bool> ExistsAsync(Guid resumeParsedDataId, CancellationToken ct = default);
    void Add(ProcessedParsedData record);
}
