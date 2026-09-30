using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.EmployerOnboarding.Application.Commands.Media;
using JobPlatform.EmployerOnboarding.Application.Queries.Media;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.EmployerOnboarding.Api.Controllers;

/// <summary>US-3.1.2-06: company media and documents.</summary>
public sealed class CompanyMediaController : EmployerControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    [HttpPost("media")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> Attach(IFormFile? file, [FromForm] MediaKind kind, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["file"] = new[] { "VAL.File.Required" } }).ToActionResult(HttpContext);
        }

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var command = new AttachCompanyMediaCommand(kind, file.FileName, file.ContentType, file.Length, stream.ToArray());
        return await Send(command,
            value => Created($"/api/v1/employers/me/media/{value.CompanyMediaId}", value), ct);
    }

    [HttpGet("media")]
    public Task<IActionResult> List(CancellationToken ct)
    {
        var query = new ListCompanyMediaQuery();
        return Send(query, ct);
    }

    [HttpDelete("media/{id:guid}")]
    public Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var command = new RemoveCompanyMediaCommand(id);
        return SendNoContent(command, ct);
    }

    [HttpPut("media/{id:guid}/primary")]
    public Task<IActionResult> SetPrimary(Guid id, CancellationToken ct)
    {
        var command = new SetPrimaryLogoCommand(id);
        return SendNoContent(command, ct);
    }
}
