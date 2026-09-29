using JobPlatform.AccountIdentity.Api.Contracts;
using JobPlatform.AccountIdentity.Application.Commands.ApiCredentials;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobPlatform.AccountIdentity.Api.Controllers;

[Route("oauth")]
[EnableRateLimiting("auth")]
public sealed class OAuthController : ApiControllerBase
{
    [HttpPost("token")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded", "application/json")]
    [ProducesResponseType<OAuthTokenResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Token(CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct);
        var result = await Sender.Send(new AuthenticateApiClientCommand(body.GrantType ?? string.Empty, body.ClientId ?? string.Empty,
            body.ClientSecret ?? string.Empty), ct);
        Response.Headers.CacheControl = "no-store";
        return result.ToActionResult(HttpContext, t => Ok(new OAuthTokenResponse(t.AccessToken, t.TokenType, t.ExpiresIn, t.Scope)));
    }

    private async Task<OAuthTokenRequest> ReadBodyAsync(CancellationToken ct)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            return new OAuthTokenRequest(form["grant_type"], form["client_id"], form["client_secret"]);
        }

        return await Request.ReadFromJsonAsync<OAuthTokenRequest>(ct) ?? new OAuthTokenRequest(null, null, null);
    }
}
