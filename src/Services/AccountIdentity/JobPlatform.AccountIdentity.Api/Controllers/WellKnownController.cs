using JobPlatform.AccountIdentity.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobPlatform.AccountIdentity.Api.Controllers;

[ApiController]
[Route(".well-known")]
public sealed class WellKnownController : ControllerBase
{
    /// <summary>Public signing keys. Every BC validates access tokens locally against this document.</summary>
    [HttpGet("jwks.json")]
    [AllowAnonymous]
    public IActionResult Jwks([FromServices] SigningKeyService keys)
    {
        Response.Headers.CacheControl = "public, max-age=300";
        return Content(keys.GetJwksJson(), "application/json");
    }

    /// <summary>
    /// Authorization-server metadata (OIDC discovery / RFC 8414) so other BCs and gateways can locate the JWKS and token endpoint
    /// and validate the issuer without hard-coded URLs. BC-03 issues tokens through client-credentials (partners, services) and its own
    /// sign-in endpoints; it does not expose the interactive OIDC authorization-code flow.
    /// </summary>
    [HttpGet("openid-configuration")]
    [HttpGet("oauth-authorization-server")]
    [AllowAnonymous]
    public IActionResult Discovery([FromServices] Microsoft.Extensions.Options.IOptions<JobPlatform.AccountIdentity.Infrastructure.Security.JwtOptions> jwt)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        Response.Headers.CacheControl = "public, max-age=300";
        return Ok(new
        {
            issuer = jwt.Value.Issuer,
            jwks_uri = $"{baseUrl}/.well-known/jwks.json",
            token_endpoint = $"{baseUrl}/oauth/token",
            grant_types_supported = new[] { "client_credentials" },
            token_endpoint_auth_methods_supported = new[] { "client_secret_post" },
            response_types_supported = new[] { "token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            scopes_supported = new[] { JobPlatform.SharedKernel.Security.Scopes.Internal, JobPlatform.SharedKernel.Security.Scopes.PartnerApi }
        });
    }
}
