using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.EmployerOnboarding.Application.DTOs.Standing;

namespace JobPlatform.EmployerOnboarding.Infrastructure.Adapters;

/// <summary>Cache-aside over ICacheStore (foundation section 10, handover section 9). Failures degrade to the database (ICacheStore itself
/// degrades to in-memory/Redis timeouts; a missing key is treated the same as a cache miss).</summary>
internal sealed class EmployerCache(ICacheStore cache) : IEmployerCache
{
    private static readonly TimeSpan StandingTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CompanyTtl = TimeSpan.FromMinutes(10);

    public Task<EmployerStandingView?> GetStandingAsync(Guid employerAccountId, CancellationToken ct = default) =>
        cache.GetJsonAsync<EmployerStandingView>(StandingKey(employerAccountId), ct);

    public Task SetStandingAsync(Guid employerAccountId, EmployerStandingView view, CancellationToken ct = default) =>
        cache.SetJsonAsync(StandingKey(employerAccountId), view, StandingTtl, ct);

    public Task<CompanyPublicInfoView?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default) =>
        cache.GetJsonAsync<CompanyPublicInfoView>(CompanyKey(employerAccountId), ct);

    public Task SetCompanyAsync(Guid employerAccountId, CompanyPublicInfoView view, CancellationToken ct = default) =>
        cache.SetJsonAsync(CompanyKey(employerAccountId), view, CompanyTtl, ct);

    public async Task InvalidateAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        await cache.RemoveAsync(StandingKey(employerAccountId), ct);
        await cache.RemoveAsync(CompanyKey(employerAccountId), ct);
    }

    private static string StandingKey(Guid employerAccountId) => $"standing:{employerAccountId}";

    private static string CompanyKey(Guid employerAccountId) => $"company:{employerAccountId}";
}
