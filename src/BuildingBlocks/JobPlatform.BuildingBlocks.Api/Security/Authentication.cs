using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using JobPlatform.BuildingBlocks.Api.Interfaces.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Results;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JobPlatform.BuildingBlocks.Api.Security;

public static class Policies
{
    public const string Authenticated = "Authenticated";
    public const string Administrator = "Administrator";
    public const string JobSeeker = "JobSeeker";
    public const string Employer = "Employer";
    public const string ExternalJobSite = "ExternalJobSite";
    public const string InternalService = "InternalService";
    public const string AnyOf_JobSeekerOrEmployer = "JobSeekerOrEmployer";
}

/// <summary>Overrides the error code returned when a policy on this endpoint refuses access (e.g. E-AUDIT-FORBIDDEN).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ForbiddenCodeAttribute : Attribute
{
    public ForbiddenCodeAttribute(string code) => Code = code;

    public string Code { get; }
}

/// <summary>Token validation settings. The token issuer is BC-03; other services only validate (no reference to its assemblies).</summary>
public sealed class JwtValidationOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://identity.jobplatform.local";
    public string Audience { get; set; } = "jobplatform";

    /// <summary>Public JWKS endpoint of BC-03 (http://account-identity-api:8080/.well-known/jwks.json). Fetched lazily and cached.</summary>
    public string? JwksUri { get; set; }

    /// <summary>Inline JWKS document (tests / air-gapped deployments). Takes precedence over <see cref="JwksUri"/>.</summary>
    public string? Jwks { get; set; }

    public int JwksCacheMinutes { get; set; } = 10;
}

/// <summary>Resolves signing keys from an inline JWKS or from BC-03's JWKS endpoint (cached; refetched once when a key id is unknown).</summary>
public sealed class SigningKeyProvider
{
    private readonly JwtValidationOptions _options;
    private readonly IHttpClientFactory _http;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private IReadOnlyList<SecurityKey> _keys = Array.Empty<SecurityKey>();
    private IReadOnlyList<SecurityKey>? _inline;
    private DateTime _loadedAt = DateTime.MinValue;

    public SigningKeyProvider(IOptions<JwtValidationOptions> options, IHttpClientFactory http, TimeProvider clock)
    {
        _options = options.Value;
        _http = http;
        _clock = clock;
    }

    public IEnumerable<SecurityKey> GetKeys(string? kid)
    {
        if (!string.IsNullOrWhiteSpace(_options.Jwks))
        {
            return _inline ??= Load(_options.Jwks);
        }

        if (string.IsNullOrWhiteSpace(_options.JwksUri))
        {
            return Array.Empty<SecurityKey>();
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var stale = now - _loadedAt > TimeSpan.FromMinutes(_options.JwksCacheMinutes);
        var unknownKid = kid is not null && !_keys.Any(k => k.KeyId == kid) && now - _loadedAt > TimeSpan.FromSeconds(30);
        if (stale || unknownKid)
        {
            try
            {
                var json = _http.CreateClient("jwks").GetStringAsync(_options.JwksUri).GetAwaiter().GetResult();
                lock (_gate)
                {
                    _keys = Load(json);
                    _loadedAt = now;
                }
            }
            catch (Exception)
            {
                // Keep serving the last known keys; a fetch failure must not lock every user out.
            }
        }

        return _keys;
    }

    private static IReadOnlyList<SecurityKey> Load(string json) => new JsonWebKeySet(json).GetSigningKeys().ToArray();
}

internal sealed class ConfigureJwtBearer : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string FailureItem = "auth-failure";

    private readonly JwtValidationOptions _jwt;
    private readonly SigningKeyProvider _keys;
    private readonly TimeProvider _clock;

    public ConfigureJwtBearer(IOptions<JwtValidationOptions> jwt, SigningKeyProvider keys, TimeProvider clock)
    {
        _jwt = jwt.Value;
        _keys = keys;
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
            IssuerSigningKeyResolver = (_, _, kid, _) => _keys.GetKeys(kid),
            NameClaimType = "sub"
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = OnChallengeAsync,
            // Browsers cannot set headers on WebSocket/SSE connections: SignalR hubs (/hubs/...) pass the token as ?access_token=.
            OnMessageReceived = context =>
            {
                if (context.Request.Path.StartsWithSegments("/hubs") && context.Request.Query["access_token"].FirstOrDefault() is { Length: > 0 } token)
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

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

    private static async Task OnChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();
        var code = context.HttpContext.Items[FailureItem] as string
                   ?? (context.AuthenticateFailure is SecurityTokenExpiredException ? "E-AAFR-SESSION-EXPIRED" : "E-AAFR-UNAUTHORIZED");
        await context.HttpContext.WriteProblemAsync(Error.Unauthorized(code, "Authentication is required."));
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

        var code = context.GetEndpoint()?.Metadata.GetMetadata<ForbiddenCodeAttribute>()?.Code ?? "E-AAFR-FORBIDDEN";
        await context.WriteProblemAsync(Error.Forbidden(code, "You are not allowed to perform this action."));
    }
}

/// <summary>Where the RBAC permission of an administrator command is evaluated.</summary>
public sealed class PermissionOptions
{
    public const string SectionName = "Authorization";

    /// <summary>"AdministratorImplicit" (default: an MFA-verified administrator holds every permission; role management is BC-03's) or
    /// "Remote" (asks BC-03's internal check-permission endpoint with a client-credentials token, cached briefly).</summary>
    public string PermissionMode { get; set; } = "AdministratorImplicit";

    public int CacheSeconds { get; set; } = 60;
}

/// <summary>Command/query-level access check (THR-031) for services that only see the JWT: actor type, MFA and (optionally) remote permission.</summary>
public sealed class ClaimsAccessAuthorizer : IAccessAuthorizer
{
    private readonly IServiceProvider _services;
    private readonly PermissionOptions _options;

    public ClaimsAccessAuthorizer(IServiceProvider services, IOptions<PermissionOptions> options)
    {
        _services = services;
        _options = options.Value;
    }

    public async Task<Result<Unit>> AuthorizeAsync(ICurrentUser user, IAuthorizedRequest request, string requestName, CancellationToken ct = default)
    {
        if (!user.IsAuthenticated || user.UserId is null && user.ClientId is null)
        {
            return Error.Unauthorized("E-AAFR-UNAUTHORIZED", "Authentication is required.");
        }

        var forbidden = Error.Forbidden(request.ForbiddenErrorCode, "You are not allowed to perform this action.");
        if (request.AllowedActorTypes.Count > 0 && (user.ActorType is null || !request.AllowedActorTypes.Contains(user.ActorType.Value)))
        {
            return forbidden;
        }

        if (request.RequireMfa && !user.MfaVerified)
        {
            return forbidden;
        }

        if (request.RequiredPermission is { } permission && user.ActorType == ActorType.Administrator
            && string.Equals(_options.PermissionMode, "Remote", StringComparison.OrdinalIgnoreCase))
        {
            var checker = _services.GetRequiredService<IRemotePermissionChecker>();
            if (user.UserId is not { } id || !await checker.IsAllowedAsync(id, permission, ct))
            {
                return forbidden;
            }
        }

        return Result.Success();
    }
}

public static class AuthenticationSetup
{
    public static IServiceCollection AddJobPlatformAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtValidationOptions>(configuration.GetSection(JwtValidationOptions.SectionName));
        services.Configure<PermissionOptions>(configuration.GetSection(PermissionOptions.SectionName));
        services.AddHttpClient("jwks");
        services.AddSingleton<SigningKeyProvider>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemAuthorizationResultHandler>();
        services.AddScoped<IAccessAuthorizer, ClaimsAccessAuthorizer>();
        services.AddSingleton<IRemotePermissionChecker, RemotePermissionChecker>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser());
            options.AddPolicy(Policies.Administrator, p => p.RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.ActorType, nameof(ActorType.Administrator))
                .RequireClaim(AppClaimTypes.Amr, AppClaimTypes.MfaValue));
            options.AddPolicy(Policies.JobSeeker, p => p.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.ActorType, nameof(ActorType.JobSeeker)));
            options.AddPolicy(Policies.Employer, p => p.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.ActorType, nameof(ActorType.Employer)));
            options.AddPolicy(Policies.ExternalJobSite, p => p.RequireAuthenticatedUser().RequireClaim(AppClaimTypes.ActorType, nameof(ActorType.ExternalJobSite)));
            options.AddPolicy(Policies.AnyOf_JobSeekerOrEmployer, p => p.RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.ActorType, nameof(ActorType.JobSeeker), nameof(ActorType.Employer)));
            options.AddPolicy(Policies.InternalService, p => p.RequireAuthenticatedUser()
                .RequireClaim(AppClaimTypes.ActorType, nameof(ActorType.System))
                .RequireAssertion(ctx => (ctx.User.FindFirstValue(AppClaimTypes.Scope) ?? string.Empty).Split(' ').Contains(Scopes.Internal)));
        });
        return services;
    }
}

/// <summary>Asks BC-03 (POST /internal/v1/accounts/{id}/check-permission) whether an administrator holds a permission; decisions are cached for a short time.</summary>
internal sealed class RemotePermissionChecker : IRemotePermissionChecker
{
    private readonly IServiceProvider _services;
    private readonly PermissionOptions _options;
    private readonly TimeProvider _clock;
    private readonly Dictionary<(Guid, string), (bool Allowed, DateTime At)> _cache = new();
    private readonly object _gate = new();
    private string? _token;
    private DateTime _tokenExpires;

    public RemotePermissionChecker(IServiceProvider services, IOptions<PermissionOptions> options, TimeProvider clock)
    {
        _services = services;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<bool> IsAllowedAsync(Guid accountId, string permission, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        lock (_gate)
        {
            if (_cache.TryGetValue((accountId, permission), out var hit) && now - hit.At < TimeSpan.FromSeconds(_options.CacheSeconds))
            {
                return hit.Allowed;
            }
        }

        var configuration = _services.GetRequiredService<IConfiguration>();
        var baseUrl = configuration["Identity:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return false;
        }

        var http = _services.GetRequiredService<IHttpClientFactory>().CreateClient("identity-internal");
        http.BaseAddress = new Uri(baseUrl);
        try
        {
            if (_token is null || _tokenExpires <= now)
            {
                var response = await http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = configuration["Identity:ClientId"] ?? string.Empty,
                    ["client_secret"] = configuration["Identity:ClientSecret"] ?? string.Empty
                }), ct);
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                _token = doc.RootElement.GetProperty("access_token").GetString();
                _tokenExpires = now.AddSeconds(Math.Max(30, doc.RootElement.GetProperty("expires_in").GetInt32() - 30));
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"/internal/v1/accounts/{accountId}/check-permission")
            {
                Content = JsonContent.Create(new { permission })
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            var check = await http.SendAsync(request, ct);
            check.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await check.Content.ReadAsStringAsync(ct));
            var allowed = body.RootElement.GetProperty("allowed").GetBoolean();
            lock (_gate)
            {
                _cache[(accountId, permission)] = (allowed, now);
            }

            return allowed;
        }
        catch (Exception)
        {
            return false; // fail closed
        }
    }
}
