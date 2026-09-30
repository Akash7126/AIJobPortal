using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AccountIdentity.Api.Controllers;

/// <summary>Controllers stay thin: map the body to a command, send it through the pipeline, map the result to HTTP. No logic, no DbContext.</summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ISender Sender
    {
        get
        {
            return HttpContext.RequestServices.GetRequiredService<ISender>();
        }
    }

    protected string? IdempotencyKey
    {
        get
        {
            return Request.Headers["Idempotency-Key"].FirstOrDefault();
        }
    }

    /// <summary>Optimistic-concurrency precondition of an update (foundation section 11). Absent = unconditional.</summary>
    protected string? IfMatch
    {
        get
        {
            var value = Request.Headers.IfMatch.ToString();
            if (!string.IsNullOrEmpty(value) && value.Length > 0)
            {
                return value;
            }

            return null;
        }
    }

    /// <summary>200 with the ETag response header (the resource's RowVersion) so clients can send it back as If-Match.</summary>
    protected IActionResult OkWithETag<T>(T body, string etag)
    {
        if (!string.IsNullOrEmpty(etag))
        {
            Response.Headers.ETag = etag;
        }

        return Ok(body);
    }

    protected async Task<IActionResult> Send<T>(IRequest<T> request, Func<T, IActionResult> onSuccess, CancellationToken ct)
    {
        var result = await Sender.Send(request, ct);
        return result.ToActionResult(HttpContext, onSuccess);
    }

    protected Task<IActionResult> SendNoContent(IRequest<Unit> request, CancellationToken ct)
    {
        return Send(request, _ => NoContent(), ct);
    }
}
