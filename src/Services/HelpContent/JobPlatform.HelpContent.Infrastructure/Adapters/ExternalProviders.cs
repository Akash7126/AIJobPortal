using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.HelpContent.Application;
using JobPlatform.HelpContent.Application.DTOs.CompanyPage;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.SharedKernel.ApiContracts.EmployerOnboarding;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.HelpContent.Infrastructure.Adapters;

// ---------------------------------------------------------------------- BC-05: company info + verification standing (handover section 6.2)

/// <summary>Wire shape of BC-05's GET /internal/v1/employers/{id}/company (not yet in a frozen SharedKernel contract, unlike the
/// standing endpoint - see JobPlatform.EmployerOnboarding.Api InternalEmployersController.Company / CompanyPublicInfoView).</summary>
internal sealed record CompanyPublicInfoDto(Guid EmployerAccountId, string Name, string? LogoUrl, string Industry, string Size, string Website);

/// <summary>Calls BC-05's company-info and standing internal APIs and merges them into one directory entry. Degrades to null (page
/// composition then returns 404) only when the company-info call itself fails; a standing-lookup failure degrades to "not verified".</summary>
internal sealed class HttpCompanyDirectoryProvider : ICompanyDirectoryProvider
{
    private readonly HttpClient _http;

    public HttpCompanyDirectoryProvider(HttpClient http) => _http = http;

    public async Task<CompanyDirectoryEntry?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        CompanyPublicInfoDto? company;
        try
        {
            company = await _http.GetOrNullAsync<CompanyPublicInfoDto>($"internal/v1/employers/{employerAccountId}/company", ct);
        }
        catch (InternalApiException)
        {
            return null;
        }

        if (company is null)
        {
            return null;
        }

        EmployerStandingDto? standing;
        try
        {
            standing = await _http.GetOrNullAsync<EmployerStandingDto>($"internal/v1/employers/{employerAccountId}/standing", ct);
        }
        catch (InternalApiException)
        {
            standing = null;
        }

        return new CompanyDirectoryEntry(company.Name, company.LogoUrl, company.Industry, company.Size, company.Website,
            standing?.Verified ?? false, standing?.Badge);
    }
}

/// <summary>Standalone fallback when BC-05 is not deployed in this environment.</summary>
internal sealed class FakeCompanyDirectoryProvider : ICompanyDirectoryProvider
{
    public Task<CompanyDirectoryEntry?> GetCompanyAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult<CompanyDirectoryEntry?>(null);
}

// ---------------------------------------------------------------------- BC-09: current job openings (Q-05, resolved - see BC-06 status doc)

internal sealed record OpenPostingTitleDto(string? Ar, string? En);

internal sealed record OpenPostingLocationDto(string? Governorate, string? City);

/// <summary>Wire shape of BC-09's GET /internal/v1/employers/{id}/open-postings (JobPlatform.JobPosting.Application.DTOs.Common.JobPostingSummaryView).</summary>
internal sealed record OpenPostingDto(Guid JobPostingId, OpenPostingTitleDto Title, OpenPostingLocationDto? Location, DateTime DeadlineUtc);

/// <summary>Calls BC-09's open-postings internal API; degrades to an empty list with Degraded=true rather than failing the whole company
/// page (handover section 6.2 "degrade: empty list + notice").</summary>
internal sealed class HttpOpenPostingsProvider : IOpenPostingsProvider
{
    private readonly HttpClient _http;

    public HttpOpenPostingsProvider(HttpClient http) => _http = http;

    public async Task<(IReadOnlyList<OpenPostingView> Items, bool Degraded)> GetOpenPostingsAsync(Guid employerAccountId, CancellationToken ct = default)
    {
        try
        {
            var page = await _http.GetOrNullAsync<PagedResult<OpenPostingDto>>($"internal/v1/employers/{employerAccountId}/open-postings?page=1&pageSize=20", ct);
            var items = (page?.Items ?? Array.Empty<OpenPostingDto>())
                .Select(p => new OpenPostingView(p.JobPostingId, p.Title.En ?? p.Title.Ar ?? string.Empty, p.Location?.Governorate, p.Location?.City, p.DeadlineUtc))
                .ToArray();
            return (items, false);
        }
        catch (InternalApiException)
        {
            return (Array.Empty<OpenPostingView>(), true);
        }
    }
}

/// <summary>Standalone fallback when BC-09 is not deployed in this environment.</summary>
internal sealed class FakeOpenPostingsProvider : IOpenPostingsProvider
{
    public Task<(IReadOnlyList<OpenPostingView> Items, bool Degraded)> GetOpenPostingsAsync(Guid employerAccountId, CancellationToken ct = default) =>
        Task.FromResult<(IReadOnlyList<OpenPostingView>, bool)>((Array.Empty<OpenPostingView>(), true));
}

// ---------------------------------------------------------------------- BC-04: profile interest tags for feed personalisation (Q-06, proposed)

internal sealed record InterestTagsDto(IReadOnlyList<string> Tags);

/// <summary>Calls BC-04's (proposed, not yet implemented anywhere - handover Q-06) profile interest-tags API; degrades to no tags
/// (the caller then falls back to the general feed, handover section 6.2).</summary>
internal sealed class HttpProfileInterestsProvider : IProfileInterestsProvider
{
    private readonly HttpClient _http;

    public HttpProfileInterestsProvider(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<string>> GetInterestTagsAsync(Guid jobSeekerAccountId, CancellationToken ct = default)
    {
        try
        {
            var dto = await _http.GetOrNullAsync<InterestTagsDto>($"internal/v1/profiles/{jobSeekerAccountId}/interest-tags", ct);
            return dto?.Tags ?? Array.Empty<string>();
        }
        catch (InternalApiException)
        {
            return Array.Empty<string>();
        }
    }
}

/// <summary>Standalone fallback when BC-04 is not deployed, or has not implemented Q-06's proposed endpoint yet.</summary>
internal sealed class FakeProfileInterestsProvider : IProfileInterestsProvider
{
    public Task<IReadOnlyList<string>> GetInterestTagsAsync(Guid jobSeekerAccountId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}
