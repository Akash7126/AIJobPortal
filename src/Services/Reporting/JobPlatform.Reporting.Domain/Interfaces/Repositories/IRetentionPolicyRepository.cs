namespace JobPlatform.Reporting.Domain.Interfaces.Repositories;

/// <summary>Aggregate-oriented repositories. The read side never uses them (see IAnalyticsQueryService in the application layer).</summary>
public interface IRetentionPolicyRepository
{
    Task<ActivityLogRetentionPolicy?> GetAsync(CancellationToken ct = default);

    void Add(ActivityLogRetentionPolicy policy);
}
