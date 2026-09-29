using JobPlatform.EmployerOnboarding.Application.Commands.Registration;
using JobPlatform.EmployerOnboarding.Application.Queries.Registration;
using JobPlatform.EmployerOnboarding.Domain;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.EmployerOnboarding.Api.Controllers;

/// <summary>US-3.1.4-04 (input side): employer submits Level-2 company details.</summary>
public sealed class RegistrationController : EmployerControllerBase
{
    public sealed record SubmitLevel2Request(
        string CompanyName, string CompanyId, string RegistrationNumber,
        string Website, string Industry, CompanySize Size, string Governorate, string City, string? Street, string Description);

    [HttpPut("registration/level2")]
    public Task<IActionResult> SubmitLevel2([FromBody] SubmitLevel2Request body, CancellationToken ct)
    {
        return Send(new SubmitEmployerLevel2Command(body.CompanyName, body.CompanyId, body.RegistrationNumber, body.Website, body.Industry, body.Size,
            body.Governorate, body.City, body.Street, body.Description), ct);
    }

    [HttpGet("registration")]
    public Task<IActionResult> Get(CancellationToken ct)
    {
        return Send(new GetEmployerRegistrationQuery(), value =>
        {
            SetETag(value.RowVersion);
            return Ok(value);
        }, ct);
    }
}
