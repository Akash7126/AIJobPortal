using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.JobPosting.Application;
using JobPlatform.JobPosting.Application.Interfaces;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Domain.Interfaces.Services;
using JobPlatform.SharedKernel.ApiContracts.EmployerOnboarding;
using JobPlatform.SharedKernel.ApiContracts.PlatformAdministration;

namespace JobPlatform.JobPosting.Infrastructure.Adapters;

/// <summary>Redis-backed cache-aside (foundation section 10); Infrastructure-only, never the source of truth.</summary>
internal sealed class JobPostingCache : IJobPostingCache
{
    private readonly ICacheStore _cache;

    public JobPostingCache(ICacheStore cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => _cache.GetJsonAsync<T>(key, ct);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => _cache.SetJsonAsync(key, value, ttl, ct);

    public Task RemoveAsync(string key, CancellationToken ct = default) => _cache.RemoveAsync(key, ct);

    /// <summary>ICacheStore exposes no prefix scan (no Redis SCAN); nothing currently needs it (only single-key eviction is used).</summary>
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>Calls BC-08's published taxonomy API (skills/jobs categories/trainings), cache-aside 60 min (handover section 9).</summary>
internal sealed class HttpTaxonomyProvider : ITaxonomyProvider
{
    private readonly HttpClient _http;
    private readonly IJobPostingCache _cache;

    public HttpTaxonomyProvider(HttpClient http, IJobPostingCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<TaxonomySnapshot> GetAsync(string type, CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync<TaxonomySnapshot>(CacheKeys.Taxonomy(type), ct);
        if (cached is not null)
        {
            return cached;
        }

        TaxonomyDto? dto;
        try
        {
            dto = await _http.GetOrNullAsync<TaxonomyDto>($"internal/v1/taxonomies/{type}", ct);
        }
        catch (InternalApiException ex)
        {
            throw new TaxonomyUnavailableException($"BC-08 taxonomy '{type}' is unavailable.", ex);
        }

        var snapshot = dto is null
            ? new TaxonomySnapshot(type, 1, Array.Empty<string>())
            : new TaxonomySnapshot(type, ParseVersion(dto.Version), dto.Entries.Select(e => e.Code).ToArray());
        await _cache.SetAsync(CacheKeys.Taxonomy(type), snapshot, CacheKeys.TaxonomyTtl, ct);
        return snapshot;
    }

    private static int ParseVersion(string version)
    {
        var digits = new string(version.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var v) ? v : 1;
    }
}

/// <summary>Standalone fallback when BC-08 is not deployed: no whitelist constraint (the schema validator then only checks required fields).</summary>
internal sealed class FakeTaxonomyProvider : ITaxonomyProvider
{
    public Task<TaxonomySnapshot> GetAsync(string type, CancellationToken ct = default) =>
        Task.FromResult(new TaxonomySnapshot(type, 1, Array.Empty<string>()));
}

/// <summary>
/// Q-02 (decided here): calls BC-05's employer-standing API when configured; the caller applies <see cref="EmployerEligibilityPolicy"/>
/// (fail-open) when this returns null.
/// </summary>
internal sealed class HttpEmployerStandingProvider : IEmployerStandingProvider
{
    private readonly HttpClient _http;
    private readonly IJobPostingCache _cache;

    public HttpEmployerStandingProvider(HttpClient http, IJobPostingCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<bool?> IsApprovedAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        var cached = await _cache.GetAsync<bool?>(CacheKeys.EmployerStanding(employerAccountId), ct);
        if (cached is not null)
        {
            return cached;
        }

        try
        {
            var dto = await _http.GetOrNullAsync<EmployerStandingDto>($"internal/v1/employers/{employerAccountId}/standing", ct);
            var approved = dto?.Approved;
            if (approved is not null)
            {
                await _cache.SetAsync(CacheKeys.EmployerStanding(employerAccountId), approved, CacheKeys.EmployerStandingTtl, ct);
            }

            return approved;
        }
        catch (InternalApiException)
        {
            return null;
        }
    }
}

/// <summary>Standalone fallback when BC-05 is not deployed: no employer-standing gate (fail-open per <see cref="EmployerEligibilityPolicy"/>).</summary>
internal sealed class FakeEmployerStandingProvider : IEmployerStandingProvider
{
    public Task<bool?> IsApprovedAsync(Guid employerAccountId, CancellationToken ct = default) => Task.FromResult<bool?>(null);
}

/// <summary>Calls BC-10's match-ranking API for personalised job recommendations (handover section 6.2, degrades to null/empty on failure).</summary>
internal sealed class HttpMatchRankingProvider : IMatchRankingProvider
{
    private readonly HttpClient _http;

    public HttpMatchRankingProvider(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<MatchRankingItemView>> GetRankingAsync(Guid profileId, int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var dto = await _http.GetOrNullAsync<MatchRankingResponse>($"internal/v1/match-ranking?profileId={profileId}&page={page}&pageSize={pageSize}", ct);
            return dto?.Items.Select(i => new MatchRankingItemView(i.JobPostingId, i.Title, i.Score)).ToArray() ?? Array.Empty<MatchRankingItemView>();
        }
        catch (InternalApiException)
        {
            return Array.Empty<MatchRankingItemView>();
        }
    }

    private sealed record MatchRankingResponse(IReadOnlyList<MatchRankingItemDto> Items);

    private sealed record MatchRankingItemDto(Guid JobPostingId, string Title, decimal Score);
}

/// <summary>Standalone fallback when BC-10 is not deployed: empty ranking (caller degrades to plain search).</summary>
internal sealed class FakeMatchRankingProvider : IMatchRankingProvider
{
    public Task<IReadOnlyList<MatchRankingItemView>> GetRankingAsync(Guid profileId, int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<MatchRankingItemView>>(Array.Empty<MatchRankingItemView>());
}

/// <summary>INV-02: validates a posting's fields against the taxonomy version in effect at submission (handover section 3.5).</summary>
internal sealed class JobPostingSchemaValidator : IJobPostingSchemaValidator
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(30);

    private readonly ITaxonomyProvider _taxonomy;
    private readonly TimeProvider _clock;

    public JobPostingSchemaValidator(ITaxonomyProvider taxonomy, TimeProvider clock)
    {
        _taxonomy = taxonomy;
        _clock = clock;
    }

    public async Task<SchemaValidationResult> ValidateAsync(JobPostingFields fields, CancellationToken ct = default)
    {
        var start = _clock.GetUtcNow();
        TaxonomySnapshot skills;
        TaxonomySnapshot categories;
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                skills = await _taxonomy.GetAsync("skills", ct);
                categories = await _taxonomy.GetAsync("jobs", ct);
                break;
            }
            catch (TaxonomyUnavailableException) when (attempt < MaxAttempts && _clock.GetUtcNow() - start < Budget)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, attempt * 3)), _clock, ct);
            }
        }

        var errors = new List<string>();
        if (skills.ValidCodes.Count > 0)
        {
            var unknown = fields.Skills.Where(s => !skills.ValidCodes.Contains(s, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (unknown.Length > 0)
            {
                errors.Add($"Unknown skill code(s): {string.Join(", ", unknown)}");
            }
        }

        if (categories.ValidCodes.Count > 0 && !categories.ValidCodes.Contains(fields.CategoryCode, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Unknown category code: {fields.CategoryCode}");
        }

        return new SchemaValidationResult(errors.Count == 0, skills.Version, errors);
    }
}
