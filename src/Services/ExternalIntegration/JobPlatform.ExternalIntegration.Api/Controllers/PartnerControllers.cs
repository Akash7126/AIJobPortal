using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.ExternalIntegration.Application.Commands.ApiFramework;
using JobPlatform.ExternalIntegration.Application.Commands.Integrations;
using JobPlatform.ExternalIntegration.Application.Commands.JobDataFlows;
using JobPlatform.ExternalIntegration.Application.Commands.Mapping;
using JobPlatform.ExternalIntegration.Application.DTOs.Mapping;
using JobPlatform.ExternalIntegration.Application.Queries.ApiFramework;
using JobPlatform.ExternalIntegration.Application.Queries.Integrations;
using JobPlatform.ExternalIntegration.Application.Queries.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.ExternalIntegration.Api.Controllers;

/// <summary>Partner (ExternalJobSite) self-service surface (handover 6.1, the platform's public partner surface). Every refusal carries
/// E-TPJPRI-FORBIDDEN unless a more specific code applies.</summary>
[Route("api/v1/partner")]
[Authorize(Policy = Policies.ExternalJobSite)]
[ForbiddenCode("E-TPJPRI-FORBIDDEN")]
public abstract class PartnerControllerBase : ApiControllerBase
{
}

/// <summary>US-3.4.1-01/05, US-2.5-02, US-3.1.3-05/13: registration, enable, sync schedule, attribution visibility, sandbox, on-demand sync.</summary>
public sealed class IntegrationController : PartnerControllerBase
{
    public sealed record RegisterRequest(string SourcePlatformName, string BaseUrl, bool RecommendedByMolPef);

    public sealed record EnableRequest(bool PullEnabled, bool PushEnabled);

    public sealed record SyncScheduleRequest(string Mode, string? Cron);

    public sealed record AttributionVisibilityRequest(string Visibility);

    [HttpPost("integrations/register")]
    public Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken ct)
    {
        var command = new RegisterExternalJobSiteCommand(body.SourcePlatformName, body.BaseUrl, body.RecommendedByMolPef);
        return Send(command,
            value => Created($"/api/v1/partner/integration", value), ct);
    }

    [HttpGet("integration")]
    public Task<IActionResult> Get(CancellationToken ct)
    {
        var query = new GetIntegrationQuery();
        return Send(query, value =>
        {
            SetETag(value.RowVersion);
            return Ok(value);
        }, ct);
    }

    [HttpPut("integration/models")]
    public Task<IActionResult> Enable([FromBody] EnableRequest body, CancellationToken ct)
    {
        var command = new EnableIntegrationCommand(body.PullEnabled, body.PushEnabled);
        return SendNoContent(command, ct);
    }

    [HttpPut("sync-schedule")]
    public Task<IActionResult> ConfigureSyncSchedule([FromBody] SyncScheduleRequest body, CancellationToken ct)
    {
        var command = new ConfigureSyncScheduleCommand(body.Mode, body.Cron);
        return SendNoContent(command, ct);
    }

    [HttpPost("sync-runs")]
    public Task<IActionResult> StartSyncRun(CancellationToken ct)
    {
        var command = new StartSyncRunCommand();
        return Send(command, value => Accepted(value), ct);
    }

    [HttpPut("attribution-visibility")]
    public Task<IActionResult> ConfigureAttributionVisibility([FromBody] AttributionVisibilityRequest body, CancellationToken ct)
    {
        var command = new ConfigureAttributionVisibilityCommand(body.Visibility);
        return SendNoContent(command, ct);
    }

    [HttpPost("sandbox")]
    public Task<IActionResult> ProvisionSandbox(CancellationToken ct)
    {
        var command = new ProvisionSandboxCommand();
        return Send(command, value => Created("", value), ct);
    }
}

/// <summary>US-3.1.3-03 (push) and US-3.1.3-09 (source edits/closures sync).</summary>
public sealed class JobDataController : PartnerControllerBase
{
    public sealed record PushJobRequest(
        string SourceJobId, string Title, string Summary, IReadOnlyList<string> Skills, string ContractType, string WorkFormat,
        DateTime ApplicationDeadline, string Location, string? SourceUrl);

    public sealed record SyncAttributionRequest(string Operation, DateTime? Deadline, string? Description);

    [HttpPost("jobs")]
    public Task<IActionResult> Push([FromBody] PushJobRequest body, CancellationToken ct)
    {
        var command = new PushJobDataCommand(body.SourceJobId, body.Title, body.Summary, body.Skills, body.ContractType, body.WorkFormat,
                body.ApplicationDeadline, body.Location, body.SourceUrl, IdempotencyKey);
        return Send(command,
            value => value.Created ? Created($"/api/v1/partner/jobs/{value.PlatformJobId}", value) : Ok(value), ct);
    }

    [HttpPatch("jobs/{platformJobId}")]
    public Task<IActionResult> SyncAttribution(string platformJobId, [FromBody] SyncAttributionRequest body, CancellationToken ct)
    {
        var command = new SyncJobPostAttributionCommand(platformJobId, body.Operation, body.Deadline, body.Description);
        return SendNoContent(command, ct);
    }
}

/// <summary>US-3.1.3-04, US-3.4.1-03: partner-configured field mapping and the platform's standard schema.</summary>
public sealed class MappingController : PartnerControllerBase
{
    public sealed record ConfigureMappingRequest(IReadOnlyList<MappingRuleInput> Rules, string StandardSchemaVersion);

    [HttpPut("mapping")]
    public Task<IActionResult> Configure([FromBody] ConfigureMappingRequest body, CancellationToken ct)
    {
        var command = new ConfigureJobDataMappingCommand(body.Rules, body.StandardSchemaVersion);
        return Send(command, ct);
    }

    [HttpGet("mapping")]
    public Task<IActionResult> Get(CancellationToken ct)
    {
        var query = new GetJobDataMappingQuery();
        return Send(query, ct);
    }

    [HttpGet("standard-schema")]
    public Task<IActionResult> StandardSchema(CancellationToken ct)
    {
        var query = new GetStandardSchemaQuery();
        return Send(query, ct);
    }
}

/// <summary>US-3.1.3-12 (credentialed schema documentation), US-4.3-02 (authorised-partner interface specs).</summary>
public sealed class PartnerDocumentationController : PartnerControllerBase
{
    [HttpGet("schema-documentation")]
    public Task<IActionResult> SchemaDocumentation([FromQuery] string version, CancellationToken ct)
    {
        var command = new ViewApiSchemaDocumentationCommand(version);
        return Send(command, value =>
        {
            if (value.Deprecated)
            {
                Response.Headers["Deprecation"] = "true";
                if (value.SunsetAtUtc is { } sunset)
                {
                    Response.Headers["Sunset"] = sunset.ToString("R");
                }
            }

            return Ok(value);
        }, ct);
    }

    [HttpGet("~/api/v1/docs/{version}/interfaces")]
    public Task<IActionResult> Interfaces(string version, CancellationToken ct)
    {
        var query = new GetSoftwareInterfaceDocumentationQuery(version);
        return Send(query, ct);
    }
}
