using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Api.Security;
using JobPlatform.AccountIdentity.Application;
using JobPlatform.AccountIdentity.Application.Accounts;
using JobPlatform.AccountIdentity.Application.ApiCredentials;
using JobPlatform.AccountIdentity.Application.Authentication;
using JobPlatform.AccountIdentity.Application.Consent;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPlatform.AccountIdentity.Api.Controllers;

[Route("api/v1/consents")]
public sealed class ConsentsController : ApiControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public Task<IActionResult> Record(RecordConsentRequest body, CancellationToken ct)
    {
        return Send(new RecordPrivacyConsentCommand(body.GuestId, body.PolicyVersion, body.Analytics, body.Preferences, body.Marketing, body.Locale, IdempotencyKey),
            dto => Ok(dto), ct);
    }

    [HttpGet("current")]
    [AllowAnonymous]
    public Task<IActionResult> Current([FromQuery] Guid? guestId, CancellationToken ct)
    {
        return Send(new GetCurrentConsentQuery(guestId), dto => Ok(dto), ct);
    }
}
