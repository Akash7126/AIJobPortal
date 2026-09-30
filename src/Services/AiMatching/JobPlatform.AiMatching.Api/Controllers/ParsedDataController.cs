using JobPlatform.AiMatching.Application.Commands.Parsing;
using JobPlatform.AiMatching.Application.Queries.Parsing;
using JobPlatform.BuildingBlocks.Api.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AiMatching.Api.Controllers;

/// <summary>The job seeker's own parsed resume data: review and correct (US-3.3.1-08/11/12).</summary>
[Route("api/v1/profiles/me/parsed-data")]
[Authorize]
public sealed class ParsedDataController : ApiControllerBase
{
    public sealed record CorrectionRequest(string Value);

    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct)
    {
        var query = new GetParsedProfileDataQuery();
        return Send(query, ct);
    }

    [HttpPut("{field}")]
    public Task<IActionResult> Correct(string field, [FromBody] CorrectionRequest body, CancellationToken ct)
    {
        var command = new CorrectParsedProfileDataCommand(field, body.Value);
        return SendNoContent(command, ct);
    }
}
