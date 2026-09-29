using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Application.Commands.Consent;
using JobPlatform.AccountIdentity.Application.Queries.Consent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
