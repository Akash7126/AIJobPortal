using JobPlatform.HelpContent.Application.DTOs.Common;
using JobPlatform.HelpContent.Application.DTOs.CompanyPage;
using JobPlatform.HelpContent.Application.Queries.CompanyPage;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.CompanyPage;

internal sealed class GetCompanyProfilePageHandler : IQueryHandler<GetCompanyProfilePageQuery, CompanyPageView>
{
    private readonly ICompanyDirectoryProvider _directory;
    private readonly IOpenPostingsProvider _openPostings;
    private readonly ICompanyProfilePageRepository _pages;
    private readonly IHelpContentCache _cache;

    public GetCompanyProfilePageHandler(ICompanyDirectoryProvider directory, IOpenPostingsProvider openPostings, ICompanyProfilePageRepository pages,
        IHelpContentCache cache)
    {
        _directory = directory;
        _openPostings = openPostings;
        _pages = pages;
        _cache = cache;
    }

    public async Task<Result<CompanyPageView>> Handle(GetCompanyProfilePageQuery request, CancellationToken ct)
    {
        if (await _cache.GetCompanyPageAsync(request.EmployerAccountId, ct) is { } cached)
        {
            return cached;
        }

        // Story AC-01: the page exists only once the employer has completed registration (BC-05 knows this fact).
        var company = await _directory.GetCompanyAsync(request.EmployerAccountId, ct);
        if (company is null)
        {
            return Error.NotFound(ErrorCodes.NotFound, "The employer has not completed registration.");
        }

        var page = await _pages.GetByEmployerAsync(request.EmployerAccountId, ct);
        var (openPostings, degraded) = await _openPostings.GetOpenPostingsAsync(request.EmployerAccountId, ct);

        var view = new CompanyPageView(request.EmployerAccountId, company.Name, company.LogoUrl, company.Industry, company.CompanySize, company.Website,
            company.Verified, company.Verified ? company.Badge : null,
            page is null ? new LocalizedView(null, null) : new LocalizedView(page.Background.Ar, page.Background.En),
            page?.Highlights ?? Array.Empty<string>(), openPostings, degraded);

        if (!degraded)
        {
            await _cache.SetCompanyPageAsync(request.EmployerAccountId, view, ct);
        }

        return view;
    }
}
