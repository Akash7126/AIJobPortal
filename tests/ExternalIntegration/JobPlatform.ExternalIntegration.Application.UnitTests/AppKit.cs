using JobPlatform.ExternalIntegration.Application.DTOs.ApiFramework;
using JobPlatform.ExternalIntegration.Application.DTOs.Integrations;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.ExternalIntegration.Application.UnitTests;

/// <summary>In-memory implementation of every repository so handlers are tested against real aggregate state (no mocking of domain
/// behaviour).</summary>
public sealed class FakeStore : IExternalJobSiteIntegrationRepository, IJobDataRepository, IJobPostAttributionRepository, IJobDataMappingRepository,
    IApiVersionRepository, ISoftwareInterfaceRepository, IPartnerCredentialRepository, IKnownPartnerAccountRepository, IApiSchemaAccessLogRepository
{
    public List<ExternalJobSiteIntegration> Integrations { get; } = new();
    public List<JobData> JobData { get; } = new();
    public List<JobPostAttribution> Attributions { get; } = new();
    public List<JobDataMapping> Mappings { get; } = new();
    public List<ApiVersion> ApiVersions { get; } = new();
    public List<SoftwareInterfaceConnection> SoftwareInterfaces { get; } = new();
    public List<PartnerCredential> Credentials { get; } = new();
    public List<KnownPartnerAccount> KnownAccounts { get; } = new();
    public List<ApiSchemaAccessLog> AccessLogs { get; } = new();

    Task<ExternalJobSiteIntegration?> IExternalJobSiteIntegrationRepository.GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Integrations.FirstOrDefault(i => i.Id == id));

    public Task<ExternalJobSiteIntegration?> GetByPartnerAccountAsync(Guid partnerAccountId, CancellationToken ct = default) =>
        Task.FromResult(Integrations.FirstOrDefault(i => i.PartnerAccountId == partnerAccountId));

    public Task<ExternalJobSiteIntegration?> GetBySourcePlatformAsync(Guid sourcePlatformId, CancellationToken ct = default) =>
        Task.FromResult(Integrations.FirstOrDefault(i => i.SourcePlatform.Id == sourcePlatformId));

    public void Add(ExternalJobSiteIntegration integration) => Integrations.Add(integration);

    Task<JobData?> IJobDataRepository.GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(JobData.FirstOrDefault(j => j.Id == id));

    public Task<JobData?> GetBySourceKeyAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default) =>
        Task.FromResult(JobData.FirstOrDefault(j => j.SourcePlatformId == sourcePlatformId && j.SourceJobId == sourceJobId));

    Task<JobData?> IJobDataRepository.GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct) =>
        Task.FromResult(JobData.FirstOrDefault(j => j.PlatformJobId == platformJobId));

    public void Add(JobData jobData) => JobData.Add(jobData);

    public Task<JobPostAttribution?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default) =>
        Task.FromResult(Attributions.FirstOrDefault(a => a.PlatformJobId == platformJobId));

    public void Add(JobPostAttribution attribution) => Attributions.Add(attribution);

    public Task<JobDataMapping?> GetByIntegrationAsync(Guid integrationId, CancellationToken ct = default) =>
        Task.FromResult(Mappings.FirstOrDefault(m => m.IntegrationId == integrationId));

    public void Add(JobDataMapping mapping) => Mappings.Add(mapping);

    public Task<ApiVersion?> GetAsync(string version, CancellationToken ct = default) => Task.FromResult(ApiVersions.FirstOrDefault(v => v.Id == version));

    public Task<IReadOnlyList<ApiVersion>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApiVersion>>(ApiVersions.ToList());

    public void Add(ApiVersion version) => ApiVersions.Add(version);

    public Task<SoftwareInterfaceConnection?> GetByKeyAsync(SoftwareInterfaceCategory category, string name, CancellationToken ct = default) =>
        Task.FromResult(SoftwareInterfaces.FirstOrDefault(c => c.Category == category && c.Name == name));

    Task<IReadOnlyList<SoftwareInterfaceConnection>> ISoftwareInterfaceRepository.ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SoftwareInterfaceConnection>>(SoftwareInterfaces.ToList());

    public void Add(SoftwareInterfaceConnection connection) => SoftwareInterfaces.Add(connection);

    public Task<PartnerCredential?> GetByIdAsync(Guid apiCredentialId, CancellationToken ct = default) =>
        Task.FromResult(Credentials.FirstOrDefault(c => c.ApiCredentialId == apiCredentialId));

    public Task<bool> HasActiveCredentialAsync(Guid accountId, DateTime nowUtc, CancellationToken ct = default) =>
        Task.FromResult(Credentials.Any(c => c.AccountId == accountId && c.ExpiresAtUtc > nowUtc));

    public void Add(PartnerCredential credential) => Credentials.Add(credential);

    public Task<KnownPartnerAccount?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        Task.FromResult(KnownAccounts.FirstOrDefault(a => a.AccountId == accountId));

    public void Add(KnownPartnerAccount account) => KnownAccounts.Add(account);

    public void Add(ApiSchemaAccessLog log) => AccessLogs.Add(log);
}

public sealed class FakeReadStore : IExternalIntegrationReadStore
{
    public Func<Guid, IntegrationView?> IntegrationByPartner { get; set; } = _ => null;
    public Func<Guid, IntegrationSummaryView?> IntegrationSummary { get; set; } = _ => null;
    public IReadOnlyList<ApiVersionView> ApiVersionList { get; set; } = Array.Empty<ApiVersionView>();
    public IReadOnlyList<SoftwareInterfaceView> SoftwareInterfaceList { get; set; } = Array.Empty<SoftwareInterfaceView>();

    public Task<IntegrationView?> GetIntegrationByPartnerAsync(Guid partnerAccountId, CancellationToken ct = default) =>
        Task.FromResult(IntegrationByPartner(partnerAccountId));

    public Task<IntegrationSummaryView?> GetIntegrationSummaryAsync(Guid sourcePlatformId, CancellationToken ct = default) =>
        Task.FromResult(IntegrationSummary(sourcePlatformId));

    public Task<IReadOnlyList<ApiVersionView>> ListApiVersionsAsync(CancellationToken ct = default) => Task.FromResult(ApiVersionList);

    public Task<ApiVersionView?> GetApiVersionAsync(string version, CancellationToken ct = default) =>
        Task.FromResult(ApiVersionList.FirstOrDefault(v => v.Version == version));

    public Task<IReadOnlyList<SoftwareInterfaceView>> ListSoftwareInterfacesAsync(CancellationToken ct = default) => Task.FromResult(SoftwareInterfaceList);
}

public sealed class FakePartnerJobFeedClient : IPartnerJobFeedClient
{
    public IReadOnlyList<PartnerJobPayload> NextPayloads { get; set; } = Array.Empty<PartnerJobPayload>();
    public bool ThrowTimeout { get; set; }

    public Task<IReadOnlyList<PartnerJobPayload>> FetchAsync(Guid sourcePlatformId, string baseUrl, CancellationToken ct = default) =>
        ThrowTimeout ? throw new TimeoutException("simulated upstream timeout") : Task.FromResult(NextPayloads);
}

public static class Kit
{
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    public static ICurrentUser User(ActorType? actor = ActorType.ExternalJobSite, Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.IsAuthenticated.Returns(actor is not null);
        user.ActorType.Returns(actor);
        user.UserId.Returns(id ?? Guid.NewGuid());
        user.MfaVerified.Returns(actor == ActorType.Administrator);
        return user;
    }
}
