using JobPlatform.HelpContent.Application.Commands.CompanyPage;
using JobPlatform.HelpContent.Domain;
using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.HelpContent.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.HelpContent.Application.Handlers.CompanyPage;

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
