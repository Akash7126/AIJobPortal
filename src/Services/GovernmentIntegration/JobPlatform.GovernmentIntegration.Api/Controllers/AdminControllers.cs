using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.GovernmentIntegration.Application.Commands.Connections;
using JobPlatform.GovernmentIntegration.Application.Commands.Migration;
using JobPlatform.GovernmentIntegration.Application.Queries.Connections;
using JobPlatform.GovernmentIntegration.Application.Queries.Migration;
using JobPlatform.GovernmentIntegration.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.GovernmentIntegration.Api.Controllers;

/// <summary>US-3.4.2-02 / US-2.5-01: administrator configuration of MoL/PEF/other source connections (handover section 6.1).</summary>
[Route("api/v1/admin/government-connections")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-GI-FORBIDDEN")]
public sealed class GovernmentConnectionsController : ApiControllerBase
{
    public sealed record ConfigureRequest(string Endpoint, string AuthMethod, string CredentialRef, bool Enabled);

    [HttpPut("{source}")]
    public Task<IActionResult> Configure(SourceSystem source, [FromBody] ConfigureRequest body, CancellationToken ct) =>
        Send(new ConfigureGovernmentSourceConnectionCommand(source, body.Endpoint, body.AuthMethod, body.CredentialRef, body.Enabled), _ => NoContent(), ct);

    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) => Send(new ListGovernmentSourceConnectionsQuery(), ct);
}

/// <summary>US-6.1-01/03/04: administrator-initiated legacy data migration (handover section 6.1).</summary>
[Route("api/v1/admin/migrations")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-GI-FORBIDDEN")]
public sealed class MigrationsController : ApiControllerBase
{
    public sealed record StartRequest(IReadOnlyList<string> Phases, bool DryRun);

    public sealed record RollbackRequest(string Reason);

    [HttpPost]
    public Task<IActionResult> Start([FromBody] StartRequest body, CancellationToken ct) =>
        Send(new StartDataMigrationCommand(body.Phases, body.DryRun), value => Accepted($"/api/v1/admin/migrations/{value.Id}", value), ct);

    [HttpPost("{id:guid}/rollback")]
    public Task<IActionResult> Rollback(Guid id, [FromBody] RollbackRequest body, CancellationToken ct) =>
        Send(new RollbackMigrationRunCommand(id, body.Reason), _ => Accepted(), ct);

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Send(new GetMigrationRunQuery(id), ct);
}

/// <summary>US-6.1-04: cleansed/deduplicated state of a migrated batch (handover section 6.1).</summary>
[Route("api/v1/admin/data-quality")]
[Authorize(Policy = Policies.Administrator)]
[ForbiddenCode("E-GI-FORBIDDEN")]
public sealed class DataQualityController : ApiControllerBase
{
    [HttpGet("{batchId:guid}")]
    public Task<IActionResult> Get(Guid batchId, CancellationToken ct) => Send(new GetDataQualityQuery(batchId), ct);
}
