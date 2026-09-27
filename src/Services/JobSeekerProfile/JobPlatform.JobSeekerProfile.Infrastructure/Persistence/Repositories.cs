using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.JobSeekerProfile.Infrastructure.Persistence;

internal sealed class ProfileRepository(JobSeekerProfileDbContext db) : IProfileRepository
{
    public Task<Profile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Full(db).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Profile?> GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        Full(db).FirstOrDefaultAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public Task<bool> ExistsByOwnerAsync(Guid ownerAccountId, CancellationToken ct = default) =>
        db.Profiles.AnyAsync(p => p.OwnerAccountId == ownerAccountId, ct);

    public void Add(Profile profile) => db.Profiles.Add(profile);

    private static IQueryable<Profile> Full(JobSeekerProfileDbContext db) => db.Profiles
        .Include(p => p.Education).Include(p => p.Experience).Include(p => p.Skills).Include(p => p.Training).Include(p => p.Certificates)
        .Include(p => p.SocialLinks);
}

internal sealed class ResumeRepository(JobSeekerProfileDbContext db) : IResumeRepository
{
    public Task<Resume?> GetCurrentByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.Resumes.FirstOrDefaultAsync(r => r.ProfileId == profileId && r.IsCurrent, ct);

    public Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Resumes.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Add(Resume resume) => db.Resumes.Add(resume);
}

internal sealed class ProfileShareLinkRepository(JobSeekerProfileDbContext db) : IProfileShareLinkRepository
{
    public Task<ProfileShareLink?> GetActiveByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.ShareLinks.FirstOrDefaultAsync(l => l.ProfileId == profileId && l.IsActive, ct);

    public Task<ProfileShareLink?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        db.ShareLinks.FirstOrDefaultAsync(l => l.Token == token, ct);

    public void Add(ProfileShareLink link) => db.ShareLinks.Add(link);
}

internal sealed class SupplementaryDocumentRepository(JobSeekerProfileDbContext db) : ISupplementaryDocumentRepository
{
    public async Task<SupplementaryDocument?> GetByHashAsync(DocumentOwnerType ownerType, Guid ownerId, string sha256, CancellationToken ct = default) =>
        (await db.Documents.Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId).ToListAsync(ct))
        .FirstOrDefault(d => d.File.Sha256 == sha256);

    public Task<IReadOnlyList<SupplementaryDocument>> ListByOwnerAsync(DocumentOwnerType ownerType, Guid ownerId, CancellationToken ct = default) =>
        db.Documents.Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId).ToListAsync(ct).ContinueWith(t => (IReadOnlyList<SupplementaryDocument>)t.Result, ct);

    public Task<SupplementaryDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);

    public void Add(SupplementaryDocument document) => db.Documents.Add(document);

    public void Remove(SupplementaryDocument document) => db.Documents.Remove(document);
}

internal sealed class JobPreferenceRepository(JobSeekerProfileDbContext db) : IJobPreferenceRepository
{
    public Task<JobPreference?> GetByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.JobPreferences.FirstOrDefaultAsync(p => p.ProfileId == profileId, ct);

    public void Add(JobPreference preference) => db.JobPreferences.Add(preference);
}

internal sealed class PrivacySettingRepository(JobSeekerProfileDbContext db) : IPrivacySettingRepository
{
    public Task<PrivacySetting?> GetByProfileAsync(Guid profileId, CancellationToken ct = default) =>
        db.PrivacySettings.FirstOrDefaultAsync(p => p.ProfileId == profileId, ct);

    public void Add(PrivacySetting setting) => db.PrivacySettings.Add(setting);
}

internal sealed class KnownAccountRepository(JobSeekerProfileDbContext db) : IKnownAccountRepository
{
    public Task<KnownAccount?> GetAsync(Guid accountId, CancellationToken ct = default) => db.KnownAccounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

    public void Add(KnownAccount account) => db.KnownAccounts.Add(account);
}

internal sealed class ProcessedParsedDataRepository(JobSeekerProfileDbContext db) : IProcessedParsedDataRepository
{
    public Task<bool> ExistsAsync(Guid resumeParsedDataId, CancellationToken ct = default) =>
        db.ProcessedParsedData.AnyAsync(p => p.ResumeParsedDataId == resumeParsedDataId, ct);

    public void Add(ProcessedParsedData record) => db.ProcessedParsedData.Add(record);
}
