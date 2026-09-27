namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
public interface IExternalJobSiteIntegrationRepository
{
    Task<ExternalJobSiteIntegration?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ExternalJobSiteIntegration?> GetByPartnerAccountAsync(Guid partnerAccountId, CancellationToken ct = default);

    Task<ExternalJobSiteIntegration?> GetBySourcePlatformAsync(Guid sourcePlatformId, CancellationToken ct = default);

    void Add(ExternalJobSiteIntegration integration);
}

public interface IJobDataRepository
{
    Task<JobData?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<JobData?> GetBySourceKeyAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default);

    Task<JobData?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default);

    void Add(JobData jobData);
}

public interface IJobPostAttributionRepository
{
    Task<JobPostAttribution?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default);

    void Add(JobPostAttribution attribution);
}

public interface IJobDataMappingRepository
{
    Task<JobDataMapping?> GetByIntegrationAsync(Guid integrationId, CancellationToken ct = default);

    void Add(JobDataMapping mapping);
}

public interface IApiVersionRepository
{
    Task<ApiVersion?> GetAsync(string version, CancellationToken ct = default);

    Task<IReadOnlyList<ApiVersion>> ListAsync(CancellationToken ct = default);

    void Add(ApiVersion version);
}

public interface ISoftwareInterfaceRepository
{
    Task<SoftwareInterfaceConnection?> GetByKeyAsync(SoftwareInterfaceCategory category, string name, CancellationToken ct = default);

    Task<IReadOnlyList<SoftwareInterfaceConnection>> ListAsync(CancellationToken ct = default);

    void Add(SoftwareInterfaceConnection connection);
}

public interface IPartnerCredentialRepository
{
    Task<PartnerCredential?> GetByIdAsync(Guid apiCredentialId, CancellationToken ct = default);

    Task<bool> HasActiveCredentialAsync(Guid accountId, DateTime nowUtc, CancellationToken ct = default);

    void Add(PartnerCredential credential);
}

public interface IKnownPartnerAccountRepository
{
    Task<KnownPartnerAccount?> GetAsync(Guid accountId, CancellationToken ct = default);

    void Add(KnownPartnerAccount account);
}

public interface IApiSchemaAccessLogRepository
{
    void Add(ApiSchemaAccessLog log);
}
