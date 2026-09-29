using JobPlatform.HelpContent.Domain.Common;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;

namespace JobPlatform.HelpContent.Application.Commands.CompanyPage;

/// <summary>The route is self-service ("PUT /companies/me/page", handover section 6.1): only an Employer actor calls it, always for their
/// own EmployerAccountId. The domain's EditBackground rule is owner-or-administrator for completeness (handover section 3.6 AC-04), but no
/// separate admin-on-behalf-of route is catalogued, so only Employer is wired here.</summary>
public sealed record EditCompanyPageCommand(string? BackgroundAr, string? BackgroundEn, IReadOnlyList<string> Highlights) : AuthenticatedCommand<Unit>
{
    public IReadOnlyCollection<ActorType> AllowedActorTypes => new[] { ActorType.Employer };

    public string ForbiddenErrorCode => ErrorCodes.CompanyPageForbidden;
}
