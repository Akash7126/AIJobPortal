using JobPlatform.JobSeekerProfile.Application.Interfaces;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Domain.Common;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using DomainProfile = JobPlatform.JobSeekerProfile.Domain.Profile;
using DomainResume = JobPlatform.JobSeekerProfile.Domain.Resume;

namespace JobPlatform.JobSeekerProfile.Application.UnitTests;

/// <summary>In-memory implementation of every repository so handlers are tested against real aggregate state.
/// Note: "DomainProfile"/"DomainResume" aliases are needed because sibling Application namespaces are also named Profile/Resume.</summary>
public sealed class FakeStore : IProfileRepository, IResumeRepository, IProfileShareLinkRepository, ISupplementaryDocumentRepository,
    IJobPreferenceRepository, IPrivacySettingRepository, IKnownAccountRepository, IProcessedParsedDataRepository
{
    public List<DomainProfile> Profiles { get; } = new();
    public List<DomainResume> Resumes { get; } = new();
    public List<ProfileShareLink> ShareLinks { get; } = new();
    public List<SupplementaryDocument> Documents { get; } = new();
    public List<JobPreference> Preferences { get; } = new();
    public List<PrivacySetting> PrivacySettings { get; } = new();
    public List<KnownAccount> KnownAccounts { get; } = new();
    public List<ProcessedParsedData> Processed { get; } = new();

    Task<DomainProfile?> IProfileRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Profiles.FirstOrDefault(p => p.Id == id));
    Task<DomainProfile?> IProfileRepository.GetByOwnerAsync(Guid ownerAccountId, CancellationToken ct) => Task.FromResult(Profiles.FirstOrDefault(p => p.OwnerAccountId == ownerAccountId));
    Task<bool> IProfileRepository.ExistsByOwnerAsync(Guid ownerAccountId, CancellationToken ct) => Task.FromResult(Profiles.Any(p => p.OwnerAccountId == ownerAccountId));
    void IProfileRepository.Add(DomainProfile profile) => Profiles.Add(profile);

    Task<DomainResume?> IResumeRepository.GetCurrentByProfileAsync(Guid profileId, CancellationToken ct) => Task.FromResult(Resumes.FirstOrDefault(r => r.ProfileId == profileId && r.IsCurrent));
    Task<DomainResume?> IResumeRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Resumes.FirstOrDefault(r => r.Id == id));
    void IResumeRepository.Add(DomainResume resume) => Resumes.Add(resume);

    Task<ProfileShareLink?> IProfileShareLinkRepository.GetActiveByProfileAsync(Guid profileId, CancellationToken ct) => Task.FromResult(ShareLinks.FirstOrDefault(l => l.ProfileId == profileId && l.IsActive));
    Task<ProfileShareLink?> IProfileShareLinkRepository.GetByTokenAsync(string token, CancellationToken ct) => Task.FromResult(ShareLinks.FirstOrDefault(l => l.Token == token));
    void IProfileShareLinkRepository.Add(ProfileShareLink link) => ShareLinks.Add(link);

    Task<SupplementaryDocument?> ISupplementaryDocumentRepository.GetByHashAsync(DocumentOwnerType ownerType, Guid ownerId, string sha256, CancellationToken ct) =>
        Task.FromResult(Documents.FirstOrDefault(d => d.OwnerType == ownerType && d.OwnerId == ownerId && d.File.Sha256 == sha256));
    Task<IReadOnlyList<SupplementaryDocument>> ISupplementaryDocumentRepository.ListByOwnerAsync(DocumentOwnerType ownerType, Guid ownerId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SupplementaryDocument>>(Documents.Where(d => d.OwnerType == ownerType && d.OwnerId == ownerId).ToList());
    Task<SupplementaryDocument?> ISupplementaryDocumentRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Documents.FirstOrDefault(d => d.Id == id));
    void ISupplementaryDocumentRepository.Add(SupplementaryDocument document) => Documents.Add(document);
    void ISupplementaryDocumentRepository.Remove(SupplementaryDocument document) => Documents.Remove(document);

    Task<JobPreference?> IJobPreferenceRepository.GetByProfileAsync(Guid profileId, CancellationToken ct) => Task.FromResult(Preferences.FirstOrDefault(p => p.ProfileId == profileId));
    void IJobPreferenceRepository.Add(JobPreference preference) => Preferences.Add(preference);

    Task<PrivacySetting?> IPrivacySettingRepository.GetByProfileAsync(Guid profileId, CancellationToken ct) => Task.FromResult(PrivacySettings.FirstOrDefault(p => p.ProfileId == profileId));
    void IPrivacySettingRepository.Add(PrivacySetting setting) => PrivacySettings.Add(setting);

    Task<KnownAccount?> IKnownAccountRepository.GetAsync(Guid accountId, CancellationToken ct) => Task.FromResult(KnownAccounts.FirstOrDefault(a => a.AccountId == accountId));
    void IKnownAccountRepository.Add(KnownAccount account) => KnownAccounts.Add(account);

    Task<bool> IProcessedParsedDataRepository.ExistsAsync(Guid resumeParsedDataId, CancellationToken ct) => Task.FromResult(Processed.Any(p => p.ResumeParsedDataId == resumeParsedDataId));
    void IProcessedParsedDataRepository.Add(ProcessedParsedData record) => Processed.Add(record);
}

public sealed class FakeAccountIdentityClient : IAccountIdentityClient
{
    public List<(Guid AccountId, string Reason, string Standing)> Requests { get; } = new();

    public Task<bool> RequestDeactivationAsync(Guid accountId, string reason, string standing, CancellationToken ct = default)
    {
        Requests.Add((accountId, reason, standing));
        return Task.FromResult(true);
    }
}

public static class Kit
{
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(ActorType actor, Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(true);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id ?? Guid.NewGuid());
        return user;
    }
}
