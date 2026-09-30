using JobPlatform.PlatformAdministration.Application.Interfaces;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.PlatformAdministration.Domain.Reference;
using JobPlatform.PlatformAdministration.Domain.Settings;
using JobPlatform.PlatformAdministration.Domain.Taxonomy;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.PlatformAdministration.Application.UnitTests;

/// <summary>In-memory implementation of every repository so handlers are tested against real state.</summary>
public sealed class FakeStore : IPlatformEntityRecordRepository, ISystemSettingRepository, IReferenceFileRepository, IPlatformTaxonomyRepository, IJobOfferingRepository
{
    public List<PlatformEntityRecord> Records { get; } = new();
    public List<SystemSetting> Settings { get; } = new();
    public List<ReferenceFile> Files { get; } = new();
    public List<PlatformTaxonomy> Taxonomies { get; } = new();
    public List<TaxonomySnapshot> Snapshots { get; } = new();
    public List<JobOffering> Offerings { get; } = new();

    Task<PlatformEntityRecord?> IPlatformEntityRecordRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Records.FirstOrDefault(r => r.Id == id));

    public Task<bool> ExistsByKeyAsync(PlatformEntityType type, string identityKey, CancellationToken ct = default) =>
        Task.FromResult(Records.Any(r => r.EntityType == type && r.IdentityKey == identityKey));

    void IPlatformEntityRecordRepository.Add(PlatformEntityRecord record) => Records.Add(record);

    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Settings.FirstOrDefault(s => s.Key == key));

    void ISystemSettingRepository.Add(SystemSetting setting) => Settings.Add(setting);

    public Task<ReferenceFile?> GetByTypeAsync(ReferenceFileType type, CancellationToken ct = default) => Task.FromResult(Files.FirstOrDefault(f => f.Type == type));

    void IReferenceFileRepository.Add(ReferenceFile file) => Files.Add(file);

    Task<PlatformTaxonomy?> IPlatformTaxonomyRepository.GetByTypeAsync(string type, CancellationToken ct) => Task.FromResult(Taxonomies.FirstOrDefault(t => t.Type == type));

    void IPlatformTaxonomyRepository.Add(PlatformTaxonomy taxonomy) => Taxonomies.Add(taxonomy);

    public void AddSnapshot(TaxonomySnapshot snapshot) => Snapshots.Add(snapshot);

    Task<JobOffering?> IJobOfferingRepository.GetByIdAsync(Guid jobPostingId, CancellationToken ct) => Task.FromResult(Offerings.FirstOrDefault(o => o.Id == jobPostingId));

    public Task<bool> ExistsAsync(Guid jobPostingId, CancellationToken ct = default) => Task.FromResult(Offerings.Any(o => o.Id == jobPostingId));

    void IJobOfferingRepository.Add(JobOffering offering) => Offerings.Add(offering);
}

public sealed class FakeCache : IReferenceDataCache
{
    public Dictionary<string, object> Items { get; } = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult(Items.TryGetValue(key, out var v) ? (T?)v : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        Items[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        Items.Remove(key);
        return Task.CompletedTask;
    }
}

public static class Kit
{
    public static readonly Guid AdminId = Guid.NewGuid();

    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(ActorType? actor = ActorType.Administrator, Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(actor is not null);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id ?? AdminId);
        user.MfaVerified.Returns(actor == ActorType.Administrator);
        return user;
    }
}
