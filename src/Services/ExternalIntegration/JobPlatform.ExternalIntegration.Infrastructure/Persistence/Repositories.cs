using JobPlatform.ExternalIntegration.Domain;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.ExternalIntegration.Infrastructure.Persistence;

internal sealed class ExternalJobSiteIntegrationRepository(ExternalIntegrationDbContext db) : IExternalJobSiteIntegrationRepository
{
    public Task<ExternalJobSiteIntegration?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Integrations.Include(i => i.SyncRuns).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<ExternalJobSiteIntegration?> GetByPartnerAccountAsync(Guid partnerAccountId, CancellationToken ct = default) =>
        db.Integrations.Include(i => i.SyncRuns).FirstOrDefaultAsync(i => i.PartnerAccountId == partnerAccountId, ct);

    public Task<ExternalJobSiteIntegration?> GetBySourcePlatformAsync(Guid sourcePlatformId, CancellationToken ct = default) =>
        db.Integrations.Include(i => i.SyncRuns).FirstOrDefaultAsync(i => i.SourcePlatform.Id == sourcePlatformId, ct);

    public void Add(ExternalJobSiteIntegration integration) => db.Integrations.Add(integration);
}

internal sealed class JobDataRepository(ExternalIntegrationDbContext db) : IJobDataRepository
{
    public Task<JobData?> GetByIdAsync(Guid id, CancellationToken ct = default) => db.JobData.FirstOrDefaultAsync(j => j.Id == id, ct);

    public Task<JobData?> GetBySourceKeyAsync(Guid sourcePlatformId, string sourceJobId, CancellationToken ct = default) =>
        db.JobData.FirstOrDefaultAsync(j => j.SourcePlatformId == sourcePlatformId && j.SourceJobId == sourceJobId, ct);

    public Task<JobData?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default) =>
        db.JobData.FirstOrDefaultAsync(j => j.PlatformJobId == platformJobId, ct);

    public void Add(JobData jobData) => db.JobData.Add(jobData);
}

internal sealed class JobPostAttributionRepository(ExternalIntegrationDbContext db) : IJobPostAttributionRepository
{
    public Task<JobPostAttribution?> GetByPlatformJobIdAsync(string platformJobId, CancellationToken ct = default) =>
        db.JobPostAttributions.FirstOrDefaultAsync(a => a.PlatformJobId == platformJobId, ct);

    public void Add(JobPostAttribution attribution) => db.JobPostAttributions.Add(attribution);
}

internal sealed class JobDataMappingRepository(ExternalIntegrationDbContext db) : IJobDataMappingRepository
{
    public Task<JobDataMapping?> GetByIntegrationAsync(Guid integrationId, CancellationToken ct = default) =>
        db.JobDataMappings.FirstOrDefaultAsync(m => m.IntegrationId == integrationId, ct);

    public void Add(JobDataMapping mapping) => db.JobDataMappings.Add(mapping);
}

internal sealed class ApiVersionRepository(ExternalIntegrationDbContext db) : IApiVersionRepository
{
    public Task<ApiVersion?> GetAsync(string version, CancellationToken ct = default) => db.ApiVersions.FirstOrDefaultAsync(v => v.Id == version, ct);

    public async Task<IReadOnlyList<ApiVersion>> ListAsync(CancellationToken ct = default) => await db.ApiVersions.ToListAsync(ct);

    public void Add(ApiVersion version) => db.ApiVersions.Add(version);
}

internal sealed class SoftwareInterfaceRepository(ExternalIntegrationDbContext db) : ISoftwareInterfaceRepository
{
    public Task<SoftwareInterfaceConnection?> GetByKeyAsync(SoftwareInterfaceCategory category, string name, CancellationToken ct = default) =>
        db.SoftwareInterfaces.FirstOrDefaultAsync(c => c.Category == category && c.Name == name, ct);

    public async Task<IReadOnlyList<SoftwareInterfaceConnection>> ListAsync(CancellationToken ct = default) =>
        await db.SoftwareInterfaces.ToListAsync(ct);

    public void Add(SoftwareInterfaceConnection connection) => db.SoftwareInterfaces.Add(connection);
}

internal sealed class PartnerCredentialRepository(ExternalIntegrationDbContext db) : IPartnerCredentialRepository
{
    public Task<PartnerCredential?> GetByIdAsync(Guid apiCredentialId, CancellationToken ct = default) =>
        db.PartnerCredentials.FirstOrDefaultAsync(c => c.ApiCredentialId == apiCredentialId, ct);

    public Task<bool> HasActiveCredentialAsync(Guid accountId, DateTime nowUtc, CancellationToken ct = default) =>
        db.PartnerCredentials.AnyAsync(c => c.AccountId == accountId && c.ExpiresAtUtc > nowUtc, ct);

    public void Add(PartnerCredential credential) => db.PartnerCredentials.Add(credential);
}

internal sealed class KnownPartnerAccountRepository(ExternalIntegrationDbContext db) : IKnownPartnerAccountRepository
{
    public Task<KnownPartnerAccount?> GetAsync(Guid accountId, CancellationToken ct = default) =>
        db.KnownPartnerAccounts.FirstOrDefaultAsync(a => a.AccountId == accountId, ct);

    public void Add(KnownPartnerAccount account) => db.KnownPartnerAccounts.Add(account);
}

internal sealed class ApiSchemaAccessLogRepository(ExternalIntegrationDbContext db) : IApiSchemaAccessLogRepository
{
    public void Add(ApiSchemaAccessLog log) => db.ApiSchemaAccessLogs.Add(log);
}
