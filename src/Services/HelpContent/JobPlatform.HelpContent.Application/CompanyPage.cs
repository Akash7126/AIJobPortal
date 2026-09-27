using FluentValidation;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.HelpContent.Application;

/// <summary>
/// US-3.1.2-05: the composed company profile page - company info/verification badge from BC-05, current job openings from BC-09 (Q-05,
/// resolved: BC-09 now exposes GET /internal/v1/employers/{id}/open-postings), and the page background this BC owns. Cache-aside 5 min
/// (handover section 9), invalidated by CompanyPageChangedDomainEvent. Unverified employer shows no badge (AC-02); BC-09 unavailable
/// degrades to an empty postings list with a notice flag rather than failing the whole page (AC-04).
/// </summary>
public sealed record GetCompanyProfilePageQuery(Guid EmployerAccountId) : IQuery<CompanyPageView>;

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

/// <summary>The route is self-service ("PUT /companies/me/page", handover section 6.1): only an Employer actor calls it, always for their
/// own EmployerAccountId. The domain's EditBackground rule is owner-or-administrator for completeness (handover section 3.6 AC-04), but no
/// separate admin-on-behalf-of route is catalogued, so only Employer is wired here.</summary>
public sealed record EditCompanyPageCommand(string? BackgroundAr, string? BackgroundEn, IReadOnlyList<string> Highlights) : AuthenticatedCommand<Unit>
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => ErrorCodes.CompanyPageForbidden;
}

public sealed class EditCompanyPageValidator : AbstractValidator<EditCompanyPageCommand>
{
    public EditCompanyPageValidator()
    {
        RuleFor(c => c.BackgroundAr).MaximumLength(CompanyProfilePage.MaxBackgroundLength).WithErrorCode("VAL.BackgroundAr.TooLong");
        RuleFor(c => c.BackgroundEn).MaximumLength(CompanyProfilePage.MaxBackgroundLength).WithErrorCode("VAL.BackgroundEn.TooLong");
        RuleFor(c => c.Highlights).Must(h => h.Count <= CompanyProfilePage.MaxHighlights).WithErrorCode("VAL.Highlights.TooMany");
    }
}

/// <summary>PUT /companies/me/page - the caller edits their own page (employer) or, for support, an administrator edits any page.
/// The target employer account is the caller's own id for an employer; an administrator must act through the same route on behalf of an
/// employer session (no separate admin-target route is catalogued by the handover for this proposed aggregate).</summary>
internal sealed class EditCompanyPageHandler : ICommandHandler<EditCompanyPageCommand, Unit>
{
    private readonly ICompanyProfilePageRepository _pages;
    private readonly ICurrentUser _user;
    private readonly TimeProvider _clock;

    public EditCompanyPageHandler(ICompanyProfilePageRepository pages, ICurrentUser user, TimeProvider clock)
    {
        _pages = pages;
        _user = user;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(EditCompanyPageCommand request, CancellationToken ct)
    {
        var employerAccountId = _user.UserId!.Value;
        var now = _clock.GetUtcNow().UtcDateTime;
        var page = await _pages.GetByEmployerAsync(employerAccountId, ct);
        if (page is null)
        {
            page = CompanyProfilePage.OpenFor(Guid.NewGuid(), employerAccountId, now);
            _pages.Add(page);
        }

        page.EditBackground(new LocalizedText(request.BackgroundAr, request.BackgroundEn), request.Highlights,
            new Domain.Common.Actor(_user.UserId ?? Guid.Empty, _user.ActorType == ActorType.Administrator), now);
        return Result.Success();
    }
}
