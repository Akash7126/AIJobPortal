using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.ExternalIntegration.Api.Controllers;

/// <summary>US-3.4.3-02/05: API version lifecycle and accepted-format configuration. Administrator/platform-operator only.</summary>
[Route("api/v1/admin/api-versions")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-APIF-ADMIN-ONLY")]
public sealed class AdminApiVersionsController : ApiControllerBase
{
    public sealed record ReleaseRequest(string Version);

    public sealed record DeprecateRequest(DateTime SunsetAtUtc);

    public sealed record ConfigureFormatRequest(IReadOnlyList<string> Formats);

    [HttpPost]
    public Task<IActionResult> Release([FromBody] ReleaseRequest body, CancellationToken ct)
    {
        var command = new ReleaseApiVersionCommand(body.Version);
        return Send(command, value => Created($"/api/v1/docs/{value.Version}", value), ct);
    }

    [HttpPost("{version}/deprecate")]
    public Task<IActionResult> Deprecate(string version, [FromBody] DeprecateRequest body, CancellationToken ct)
    {
        var command = new DeprecateApiVersionCommand(version, body.SunsetAtUtc);
        return SendNoContent(command, ct);
    }

    [HttpPost("{version}/retire")]
    public Task<IActionResult> Retire(string version, CancellationToken ct)
    {
        var command = new RetireApiVersionCommand(version);
        return SendNoContent(command, ct);
    }

    [HttpPut("{version}/format")]
    public Task<IActionResult> ConfigureFormat(string version, [FromBody] ConfigureFormatRequest body, CancellationToken ct)
    {
        var command = new ConfigureApiDataFormatCommand(version, body.Formats);
        return SendNoContent(command, ct);
    }
}
