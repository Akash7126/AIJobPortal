using System.Text.Json;
using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.BuildingBlocks.Api.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.Notification.Application.Commands.Delivery;
using JobPlatform.Notification.Application.Interfaces;
using JobPlatform.Notification.Application.Queries.Delivery;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Notification.Api.Controllers;

/// <summary>Provider webhooks (SMS and e-mail delivery reports). Signed with HMAC-SHA256 in X-Signature; an unsigned or wrongly signed call is 401 (3.6.3-04).</summary>
[Route("api/v1/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController : ApiControllerBase
{
    public const string SignatureHeader = "X-Signature";

    [HttpPost("sms-delivery")]
    public Task<IActionResult> SmsDelivery(CancellationToken ct)
    {
        return Report(ct);
    }

    [HttpPost("email-events")]
    public Task<IActionResult> EmailEvents(CancellationToken ct)
    {
        return Report(ct);
    }

    private async Task<IActionResult> Report(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(ct);
        var verifier = HttpContext.RequestServices.GetRequiredService<IWebhookVerifier>();
        if (!verifier.IsValid(raw, Request.Headers[SignatureHeader].FirstOrDefault()))
        {
            return Error.Unauthorized("E-AAFR-UNAUTHORIZED", "The webhook signature is invalid.").ToActionResult(HttpContext);
        }

        string? providerMessageId;
        string? status;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            providerMessageId = doc.RootElement.TryGetProperty("providerMessageId", out var id) ? id.GetString() : null;
            status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
        }
        catch (JsonException)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["body"] = new[] { "VAL.INVALID_FORMAT" } }).ToActionResult(HttpContext);
        }

        return await Send(new RecordDeliveryReportCommand(providerMessageId ?? string.Empty, status ?? string.Empty), _ => NoContent(), ct);
    }
}

/// <summary>Service-to-service API for BC-03 (transactional SMS, Q-04) and BC-07 (masked detail backfill). Client-credentials token with the internal scope.</summary>
[Route("internal/v1/notifications")]
[Authorize(Policy = Policies.InternalService)]
public sealed class InternalNotificationsController : ApiControllerBase
{
    public sealed record OtpBody(Guid AccountId, string Purpose, string Text);

    [HttpPost("otp")]
    public async Task<IActionResult> SendOtp([FromBody] OtpBody body, CancellationToken ct)
    {
        return await Send(new SendTransactionalSmsCommand(body.AccountId, body.Purpose, body.Text, IdempotencyKey), result => Accepted(result), ct);
    }

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        return Send(new GetNotificationDetailQuery(id), ct);
    }
}
