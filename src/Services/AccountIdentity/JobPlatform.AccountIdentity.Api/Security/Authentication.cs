using System.Security.Claims;
using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JobPlatform.AccountIdentity.Api.Security;

public static class Policies
{
    public const string Authenticated = "Authenticated";
    public const string AuthenticatedAllowingPasswordChange = "AuthenticatedAllowingPasswordChange";
    public const string Administrator = "Administrator";
    public const string ExternalJobSite = "ExternalJobSite";
    public const string InternalService = "InternalService";
}

/// <summary>Overrides the error code returned when a policy on this endpoint refuses access (e.g. E-AUM-FORBIDDEN on admin account routes).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ForbiddenCodeAttribute : Attribute
{
    public ForbiddenCodeAttribute(string code) => Code = code;

    public string Code { get; }
}

/// <summary>A user whose credentials were reset must change the password before using anything else.</summary>
internal sealed class NoPendingPasswordChangeRequirement : IAuthorizationRequirement
{
    public const string FailureReason = "password-change-required";
}

internal sealed class NoPendingPasswordChangeHandler : AuthorizationHandler<NoPendingPasswordChangeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, NoPendingPasswordChangeRequirement requirement)
    {
        if (context.User.FindFirstValue(AppClaimTypes.MustChangePassword) == "true")
        {
            context.Fail(new AuthorizationFailureReason(this, NoPendingPasswordChangeRequirement.FailureReason));
        }
        else
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal static class AuthorizationSetup
{
    public static void AddPolicies(AuthorizationOptions options)
    {
        var noPending = new NoPendingPasswordChangeRequirement();

        options.AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser().AddRequirements(noPending));
        options.AddPolicy(Policies.AuthenticatedAllowingPasswordChange, p => p.RequireAuthenticatedUser());
        options.AddPolicy(Policies.Administrator, p => p.RequireAuthenticatedUser()
            .RequireClaim(AppClaimTypes.ActorType, nameof(JobPlatform.SharedKernel.Common.Enums.ActorType.Administrator))
            .RequireClaim(AppClaimTypes.Amr, AppClaimTypes.MfaValue)
            .AddRequirements(noPending));
        options.AddPolicy(Policies.ExternalJobSite, p => p.RequireAuthenticatedUser()
            .RequireClaim(AppClaimTypes.ActorType, nameof(JobPlatform.SharedKernel.Common.Enums.ActorType.ExternalJobSite))
            .AddRequirements(noPending));
        options.AddPolicy(Policies.InternalService, p => p.RequireAuthenticatedUser()
            .RequireClaim(AppClaimTypes.ActorType, nameof(JobPlatform.SharedKernel.Common.Enums.ActorType.System))
            .RequireAssertion(ctx => (ctx.User.FindFirstValue(AppClaimTypes.Scope) ?? string.Empty).Split(' ').Contains(Scopes.Internal)));
    }
}

/// <summary>Renders authorisation failures as problem+json with the documented error code instead of an empty 403.</summary>
internal sealed class ProblemAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var passwordChange = authorizeResult.AuthorizationFailure?.FailureReasons.Any(r => r.Message == NoPendingPasswordChangeRequirement.FailureReason) == true;
        var code = passwordChange
            ? "E-AAFR-PASSWORD-CHANGE-REQUIRED"
            : context.GetEndpoint()?.Metadata.GetMetadata<ForbiddenCodeAttribute>()?.Code ?? ErrorCodes.AuthForbidden;
        await context.WriteProblemAsync(Error.Forbidden(code, "You are not allowed to perform this action."));
    }
}

/// <summary>
/// JWT bearer setup. Signing keys come from the local key service (validated locally, no network call). After signature and lifetime
/// checks the session is looked up and touched (sliding idle timeout, immediate logout) and revoked tokens/clients are rejected.
/// </summary>
internal sealed class ConfigureJwtBearer : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string FailureItem = "auth-failure";

    private readonly SigningKeyService _keys;
    private readonly JwtOptions _jwt;
    private readonly TimeProvider _clock;

    public ConfigureJwtBearer(SigningKeyService keys, IOptions<JwtOptions> jwt, TimeProvider clock)
    {
        _keys = keys;
        _jwt = jwt.Value;
        _clock = clock;
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(5),
            LifetimeValidator = ValidateLifetime,
            IssuerSigningKeyResolver = (_, _, _, _) => _keys.GetValidationKeys(),
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = OnTokenValidatedAsync,
            OnChallenge = OnChallengeAsync
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    /// <summary>Lifetime check against the injected clock (deterministic tests, one time source for the whole service).</summary>
    private bool ValidateLifetime(DateTime? notBefore, DateTime? expires, SecurityToken token, TokenValidationParameters parameters)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (notBefore is { } nbf && nbf > now + parameters.ClockSkew)
        {
            throw new SecurityTokenNotYetValidException($"The token is not valid before {nbf:O}.") { NotBefore = nbf };
        }

        if (expires is { } exp && exp < now - parameters.ClockSkew)
        {
            throw new SecurityTokenExpiredException($"The token expired at {exp:O}.") { Expires = exp };
        }

        return true;
    }

    private static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        var services = context.HttpContext.RequestServices;
        var sessions = services.GetRequiredService<ISessionStore>();
        var clock = services.GetRequiredService<TimeProvider>();
        var principal = context.Principal!;
        var ct = context.HttpContext.RequestAborted;

        if (principal.FindFirstValue("jti") is { } jti && await sessions.IsTokenRevokedAsync(jti, ct))
        {
            Fail(context, ErrorCodes.AuthSessionExpired);
            return;
        }

        if (Guid.TryParse(principal.FindFirstValue(AppClaimTypes.SessionId), out var sessionId))
        {
            var session = await sessions.GetAsync(sessionId, ct);
            if (session is null || !session.IsUsable(clock))
            {
                Fail(context, ErrorCodes.AuthSessionExpired);
                return;
            }

            // Activity resets the idle timer (US-3.1.5-04 AC-02).
            session.Touch(clock);
            await sessions.SaveAsync(session, ct);
            return;
        }

        if (principal.FindFirstValue(AppClaimTypes.ClientId) is { } clientId
            && principal.FindFirstValue(AppClaimTypes.ActorType) != nameof(JobPlatform.SharedKernel.Common.Enums.ActorType.System)
            && await sessions.IsClientRevokedAsync(clientId, ct))
        {
            Fail(context, ErrorCodes.PartnerRevoked);
        }
    }

    private static void Fail(TokenValidatedContext context, string code)
    {
        context.HttpContext.Items[FailureItem] = code;
        context.Fail("rejected");
    }

    private static async Task OnChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        var code = context.HttpContext.Items[FailureItem] as string
                   ?? (context.AuthenticateFailure is SecurityTokenExpiredException ? ErrorCodes.ApiExpired : ErrorCodes.AuthUnauthorized);
        await context.HttpContext.WriteProblemAsync(Error.Unauthorized(code, "Authentication is required."));
    }
}
