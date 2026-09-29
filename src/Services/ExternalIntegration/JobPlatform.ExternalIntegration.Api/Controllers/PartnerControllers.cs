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
    public Task<IActionResult> Register([FromBody] RegisterRequest body, CancellationToken ct) =>
        Send(new RegisterExternalJobSiteCommand(body.SourcePlatformName, body.BaseUrl, body.RecommendedByMolPef),
            value => Created($"/api/v1/partner/integration", value), ct);

    [HttpGet("integration")]
    public Task<IActionResult> Get(CancellationToken ct) => Send(new GetIntegrationQuery(), value =>
    {
        SetETag(value.RowVersion);
        return Ok(value);
    }, ct);

    [HttpPut("integration/models")]
    public Task<IActionResult> Enable([FromBody] EnableRequest body, CancellationToken ct) =>
        SendNoContent(new EnableIntegrationCommand(body.PullEnabled, body.PushEnabled), ct);

    [HttpPut("sync-schedule")]
    public Task<IActionResult> ConfigureSyncSchedule([FromBody] SyncScheduleRequest body, CancellationToken ct) =>
        SendNoContent(new ConfigureSyncScheduleCommand(body.Mode, body.Cron), ct);

    [HttpPost("sync-runs")]
    public Task<IActionResult> StartSyncRun(CancellationToken ct) => Send(new StartSyncRunCommand(), value => Accepted(value), ct);

    [HttpPut("attribution-visibility")]
    public Task<IActionResult> ConfigureAttributionVisibility([FromBody] AttributionVisibilityRequest body, CancellationToken ct) =>
        SendNoContent(new ConfigureAttributionVisibilityCommand(body.Visibility), ct);

    [HttpPost("sandbox")]
    public Task<IActionResult> ProvisionSandbox(CancellationToken ct) => Send(new ProvisionSandboxCommand(), value => Created("", value), ct);
}

/// <summary>US-3.1.3-03 (push) and US-3.1.3-09 (source edits/closures sync).</summary>
public sealed class JobDataController : PartnerControllerBase
{
    public sealed record PushJobRequest(
        string SourceJobId, string Title, string Summary, IReadOnlyList<string> Skills, string ContractType, string WorkFormat,
        DateTime ApplicationDeadline, string Location, string? SourceUrl);

    public sealed record SyncAttributionRequest(string Operation, DateTime? Deadline, string? Description);

    [HttpPost("jobs")]
    public Task<IActionResult> Push([FromBody] PushJobRequest body, CancellationToken ct) =>
        Send(new PushJobDataCommand(body.SourceJobId, body.Title, body.Summary, body.Skills, body.ContractType, body.WorkFormat,
                body.ApplicationDeadline, body.Location, body.SourceUrl, IdempotencyKey),
            value => value.Created ? Created($"/api/v1/partner/jobs/{value.PlatformJobId}", value) : Ok(value), ct);

    [HttpPatch("jobs/{platformJobId}")]
    public Task<IActionResult> SyncAttribution(string platformJobId, [FromBody] SyncAttributionRequest body, CancellationToken ct) =>
        SendNoContent(new SyncJobPostAttributionCommand(platformJobId, body.Operation, body.Deadline, body.Description), ct);
}

/// <summary>US-3.1.3-04, US-3.4.1-03: partner-configured field mapping and the platform's standard schema.</summary>
public sealed class MappingController : PartnerControllerBase
{
    public sealed record ConfigureMappingRequest(IReadOnlyList<MappingRuleInput> Rules, string StandardSchemaVersion);

    [HttpPut("mapping")]
    public Task<IActionResult> Configure([FromBody] ConfigureMappingRequest body, CancellationToken ct) =>
        Send(new ConfigureJobDataMappingCommand(body.Rules, body.StandardSchemaVersion), ct);

    [HttpGet("mapping")]
    public Task<IActionResult> Get(CancellationToken ct) => Send(new GetJobDataMappingQuery(), ct);

    [HttpGet("standard-schema")]
    public Task<IActionResult> StandardSchema(CancellationToken ct) => Send(new GetStandardSchemaQuery(), ct);
}

/// <summary>US-3.1.3-12 (credentialed schema documentation), US-4.3-02 (authorised-partner interface specs).</summary>
public sealed class PartnerDocumentationController : PartnerControllerBase
{
    [HttpGet("schema-documentation")]
    public Task<IActionResult> SchemaDocumentation([FromQuery] string version, CancellationToken ct) =>
        Send(new ViewApiSchemaDocumentationCommand(version), value =>
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

    [HttpGet("~/api/v1/docs/{version}/interfaces")]
    public Task<IActionResult> Interfaces(string version, CancellationToken ct) => Send(new GetSoftwareInterfaceDocumentationQuery(version), ct);
}
