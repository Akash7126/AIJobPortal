using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.GovernmentIntegration.Application.Commands.Verifications;
using JobPlatform.GovernmentIntegration.Application.Queries.Connections;
using JobPlatform.GovernmentIntegration.Application.Queries.Migration;
using JobPlatform.GovernmentIntegration.Application.Queries.Verifications;
using JobPlatform.GovernmentIntegration.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.GovernmentIntegration.Api.Controllers;

/// <summary>Synchronous contract for other BCs (foundation section 9.5, handover section 6.1): scope gov.verify, service-to-service JWT only.
/// Never exposed by the gateway.</summary>
[Route("internal/v1")]
[Authorize(Policy = Policies.InternalService)]
[ForbiddenCode("E-GDI-FORBIDDEN")]
public sealed class InternalGovernmentIntegrationController : ApiControllerBase
{
    public sealed record RequestGovernmentVerificationRequest(string RequestingComponent, SubjectType SubjectType, Guid SubjectId, SourceSystem Source,
        AccessPurpose Purpose);

    public sealed record RequestEducationalVerificationRequest(string RequestingComponent, Guid SubjectId, string Institution, string CredentialName, int Year);

    public sealed record RequestIdentityVerificationRequest(
        string RequestingComponent, SubjectType SubjectType, Guid SubjectId, string NationalIdReference, string FullName, DateOnly DateOfBirth);

    [HttpPost("gov-verifications")]
    public Task<IActionResult> RequestGovernmentVerification([FromBody] RequestGovernmentVerificationRequest body, CancellationToken ct)
    {
        return Send(new RequestGovernmentVerificationCommand(body.RequestingComponent, body.SubjectType, body.SubjectId, body.Source, body.Purpose),
            id => Accepted((string?)null, id), ct);
    }

    [HttpPost("educational-verifications")]
    public Task<IActionResult> RequestEducationalVerification([FromBody] RequestEducationalVerificationRequest body, CancellationToken ct)
    {
        return Send(new RequestEducationalCredentialVerificationCommand(body.RequestingComponent, body.SubjectId, body.Institution, body.CredentialName, body.Year),
            id => Accepted((string?)null, id), ct);
    }

    [HttpPost("identity-verifications")]
    public Task<IActionResult> RequestIdentityVerification([FromBody] RequestIdentityVerificationRequest body, CancellationToken ct)
    {
        return Send(new RequestIdentityVerificationCommand(body.RequestingComponent, body.SubjectType, body.SubjectId, body.NationalIdReference, body.FullName,
            body.DateOfBirth), id => Accepted((string?)null, id), ct);
    }

    [HttpGet("verification-status")]
    public Task<IActionResult> VerificationStatus([FromQuery] SubjectType subjectType, [FromQuery] Guid subjectId, CancellationToken ct)
    {
        return Send(new GetSubjectVerificationStatusQuery(subjectType, subjectId), ct);
    }

    [HttpGet("government-systems")]
    public Task<IActionResult> GovernmentSystems(CancellationToken ct)
    {
        return Send(new GetGovernmentSystemsQuery(), ct);
    }

    [HttpGet("government-exchanges")]
    public Task<IActionResult> GovernmentExchanges([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        return Send(new ListGovernmentExchangesQuery(from, to, page, pageSize), ct);
    }
}
