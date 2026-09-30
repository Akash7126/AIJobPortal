namespace JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories (foundation section 8): no IQueryable leaks, no SaveChanges (the unit of work commits).</summary>
public interface IExternalJobSiteIntegrationRepository
{
    Task<ExternalJobSiteIntegration?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ExternalJobSiteIntegration?> GetByPartnerAccountAsync(Guid partnerAccountId, CancellationToken ct = default);

    Task<ExternalJobSiteIntegration?> GetBySourcePlatformAsync(Guid sourcePlatformId, CancellationToken ct = default);

    void Add(ExternalJobSiteIntegration integration);
}
