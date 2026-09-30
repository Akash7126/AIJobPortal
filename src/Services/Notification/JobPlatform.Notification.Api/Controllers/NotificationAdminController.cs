using JobPlatform.BuildingBlocks.Api.Hosting;
using JobPlatform.Notification.Application.Commands.Admin;
using JobPlatform.Notification.Application.Queries.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.Notification.Api.Controllers;

/// <summary>Administrator configuration: e-mail templates, notification types, essential SMS categories, SMS delivery status.</summary>
[Route("api/v1/admin")]
[Authorize]
public sealed class NotificationAdminController : ApiControllerBase
{
    public sealed record TemplateBody(string Subject, string Body, Dictionary<string, string>? Placeholders);

    public sealed record TypeBody(string Code, string Icon, string Colour, string TextAlternative, bool IsMandatory);

    public sealed record EssentialBody(List<string>? Categories);

    [HttpGet("email-templates/{code}")]
    public Task<IActionResult> GetTemplate(string code, [FromQuery] string locale = "en", CancellationToken ct = default)
    {
        var query = new GetEmailTemplateQuery(code, locale);
        return Send(query, ct);
    }

    [HttpPut("email-templates/{code}")]
    public Task<IActionResult> PutTemplate(string code, [FromBody] TemplateBody body, [FromQuery] string locale = "en", CancellationToken ct = default)
    {
        var command = new EditEmailTemplateCommand(code, locale, body.Subject, body.Body, body.Placeholders ?? new Dictionary<string, string>());
        return Send(command, ct);
    }

    [HttpGet("notification-types")]
    public Task<IActionResult> ListTypes(CancellationToken ct)
    {
        var query = new ListNotificationTypesQuery();
        return Send(query, ct);
    }

    [HttpPut("notification-types")]
    public Task<IActionResult> PutType([FromBody] TypeBody body, CancellationToken ct)
    {
        var command = new DefineNotificationTypeCommand(body.Code, body.Icon, body.Colour, body.TextAlternative, body.IsMandatory);
        return Send(command, ct);
    }

    [HttpGet("sms/essential-categories")]
    public Task<IActionResult> GetEssential(CancellationToken ct)
    {
        var query = new GetEssentialSmsCategoriesQuery();
        return Send(query, ct);
    }

    [HttpPut("sms/essential-categories")]
    public Task<IActionResult> PutEssential([FromBody] EssentialBody body, CancellationToken ct)
    {
        var command = new ConfigureEssentialSmsCategoriesCommand(body.Categories ?? new List<string>());
        return Send(command, ct);
    }

    [HttpGet("sms/deliveries")]
    public Task<IActionResult> Deliveries([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new ListSmsDeliveryStatusQuery(status, page, pageSize);
        return Send(query, ct);
    }
}
