using JobPlatform.CandidateSourcing.Application;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.CandidateSourcing.Api.Controllers;

[Route("api/v1/candidates/profiles")]
[Authorize]
public sealed class CandidateProfilesController : ApiControllerBase
{
    [HttpGet("me")]
    public Task<IActionResult> MyProfile(CancellationToken ct)
    {
        return Send(new GetMyProfileQuery(), ct);
    }

    [HttpPut("me")]
    public Task<IActionResult> UpdateMyProfile([FromBody] UpdateMyProfileRequest body, CancellationToken ct)
    {
        return SendNoContent(new UpdateMyProfileCommand(body.Name, body.Skills, body.Location), ct);
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Send(new GetCandidateProfileQuery(id), ct);
    }
}
