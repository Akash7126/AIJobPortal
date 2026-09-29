using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Notification.Application.Commands.InApp;
using JobPlatform.Notification.Application.Commands.Preferences;
using JobPlatform.Notification.Application.Queries.InApp;
using JobPlatform.Notification.Application.Queries.Preferences;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Notification.Api.Controllers;

/// <summary>Notification center of the signed-in user (own notifications only).</summary>
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Send(new ListInAppNotificationsQuery(status, page, pageSize), ct);

    [HttpPost("{id:guid}/read")]
    public Task<IActionResult> Read(Guid id, CancellationToken ct) => SendNoContent(new MarkNotificationReadCommand(id), ct);

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) => SendNoContent(new DeleteNotificationCommand(id), ct);

    [HttpPost("{id:guid}/action")]
    public Task<IActionResult> TakeAction(Guid id, CancellationToken ct) => Send(new TakeNotificationActionCommand(id), ct);
}

/// <summary>Channel preferences of the signed-in user: e-mail, in-app and SMS opt-in.</summary>
[Route("api/v1/notification-preferences")]
[Authorize]
public sealed class PreferencesController : ApiControllerBase
{
    public sealed record EmailBody(Dictionary<string, bool>? Categories, string? Mode);

    public sealed record InAppBody(Dictionary<string, bool>? Categories);

    public sealed record SmsBody(string? Mobile, bool OptIn);

    [HttpGet("{channel}")]
    public Task<IActionResult> Get(string channel, CancellationToken ct) => Send(new GetNotificationPreferencesQuery(channel), ct);

    [HttpPut("email")]
    public Task<IActionResult> PutEmail([FromBody] EmailBody body, CancellationToken ct) =>
        Send(new SetEmailPreferenceCommand(body.Categories ?? new Dictionary<string, bool>(), body.Mode ?? "Immediate"), ct);

    [HttpPut("in-app")]
    public Task<IActionResult> PutInApp([FromBody] InAppBody body, CancellationToken ct) =>
        Send(new SetInAppPreferenceCommand(body.Categories ?? new Dictionary<string, bool>()), ct);

    [HttpPut("sms")]
    public Task<IActionResult> PutSms([FromBody] SmsBody body, CancellationToken ct) => Send(new SetSmsOptInCommand(body.Mobile, body.OptIn), ct);
}

/// <summary>One-click unsubscribe with the signed token from an e-mail (no login).</summary>
[Route("api/v1/unsubscribe")]
[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("public")]
public sealed class UnsubscribeController : ApiControllerBase
{
    [HttpGet("{token}")]
    public Task<IActionResult> Get(string token, CancellationToken ct) => Send(new UnsubscribeCommand(token), ct);

    [HttpPost("{token}")]
    public Task<IActionResult> Post(string token, CancellationToken ct) => Send(new UnsubscribeCommand(token), _ => NoContent(), ct);
}
