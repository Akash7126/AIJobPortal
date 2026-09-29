using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.HelpContent.Application.Commands.CompanyPage;
using JobPlatform.HelpContent.Application.Queries.CompanyPage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.HelpContent.Api.Controllers;

/// <summary>US-3.1.2-05: the employer's public company profile page - anonymous read, employer self-service write.</summary>
[Route("api/v1/companies")]
public sealed class CompanyPageController : ApiControllerBase
{
    /// <summary>Composed at query time from BC-05 (company info/verification badge) and BC-09 (current job openings), plus the page
    /// background this BC owns. Unverified employer shows no badge; BC-09 unavailable degrades to an empty postings list.</summary>
    [HttpGet("{employerAccountId:guid}/page")]
    public Task<IActionResult> Get(Guid employerAccountId, CancellationToken ct) => Send(new GetCompanyProfilePageQuery(employerAccountId), ct);

    public sealed record EditPageRequest(string? BackgroundAr, string? BackgroundEn, IReadOnlyList<string> Highlights);

    [HttpPut("me/page")]
    [Authorize(Policy = Policies.Employer)]
    [ForbiddenCode("E-HCCP-FORBIDDEN")]
    public Task<IActionResult> EditMine([FromBody] EditPageRequest body, CancellationToken ct) =>
        SendNoContent(new EditCompanyPageCommand(body.BackgroundAr, body.BackgroundEn, body.Highlights), ct);
}
