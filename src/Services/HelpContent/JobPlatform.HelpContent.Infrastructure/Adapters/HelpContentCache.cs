using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.HelpContent.Application;

namespace JobPlatform.HelpContent.Infrastructure.Adapters;

/// <summary>Cache-aside over ICacheStore (foundation section 10, handover section 9): composed company page, 5 min TTL, invalidated by
/// CompanyPageChangedDomainEvent.</summary>
internal sealed class HelpContentCache(ICacheStore cache) : IHelpContentCache
{
    private static readonly TimeSpan CompanyPageTtl = TimeSpan.FromMinutes(5);

    public Task<CompanyPageView?> GetCompanyPageAsync(Guid employerAccountId, CancellationToken ct = default) =>
        cache.GetJsonAsync<CompanyPageView>(Key(employerAccountId), ct);

    public Task SetCompanyPageAsync(Guid employerAccountId, CompanyPageView view, CancellationToken ct = default) =>
        cache.SetJsonAsync(Key(employerAccountId), view, CompanyPageTtl, ct);

    public Task InvalidateCompanyPageAsync(Guid employerAccountId, CancellationToken ct = default) => cache.RemoveAsync(Key(employerAccountId), ct);

    private static string Key(Guid employerAccountId) => $"company:{employerAccountId}";
}
