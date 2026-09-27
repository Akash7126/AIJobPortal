using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JobPlatform.TestSupport;

/// <summary>
/// Issues access tokens the way BC-03 does (RS256, same claim names) so the services under test validate them through the real
/// JWT pipeline against an inline JWKS. No reference to BC-03 assemblies.
/// </summary>
public static class TestTokens
{
    public const string Issuer = "https://identity.jobplatform.local";
    public const string Audience = "jobplatform";
    public const string KeyId = "test-key-1";

    private static readonly RSA Rsa = RSA.Create(2048);

    // dotnet test runs every BC's Api.IntegrationTests collection in parallel within one process, and they all share this
    // static key (same issuer/audience/JWKS everywhere). RSA (RSACng on Windows in particular) is not guaranteed safe for
    // concurrent operations on one instance, so signing/exporting must be serialized or parallel runs intermittently produce
    // a corrupt signature (SecurityTokenInvalidSignatureException) on an otherwise-valid token.
    private static readonly object RsaGate = new();

    public static string JwksJson
    {
        get
        {
            lock (RsaGate)
            {
                var publicKey = new RsaSecurityKey(Rsa.ExportParameters(false)) { KeyId = KeyId };
                var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
                jwk.Alg = SecurityAlgorithms.RsaSha256;
                jwk.Use = "sig";
                return new JsonObject { ["keys"] = new JsonArray(JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new
                {
                    kty = jwk.Kty, kid = jwk.Kid, use = jwk.Use, alg = jwk.Alg, n = jwk.N, e = jwk.E
                }))) }.ToJsonString();
            }
        }
    }

    public static string Issue(ActorType actorType, Guid? userId = null, bool mfa = false, string? scope = null, string? clientId = null,
        TimeSpan? lifetime = null, DateTime? nowUtc = null, IEnumerable<Guid>? roleIds = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, (userId ?? Guid.NewGuid()).ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(AppClaimTypes.ActorType, actorType.ToString())
        };
        if (mfa)
        {
            claims.Add(new Claim(AppClaimTypes.Amr, AppClaimTypes.MfaValue));
        }

        if (scope is not null)
        {
            claims.Add(new Claim(AppClaimTypes.Scope, scope));
        }

        if (clientId is not null)
        {
            claims.Add(new Claim(AppClaimTypes.ClientId, clientId));
        }

        foreach (var role in roleIds ?? Array.Empty<Guid>())
        {
            claims.Add(new Claim(AppClaimTypes.RoleId, role.ToString()));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(claims),
            // Far in the past: the service validates against the factory's frozen fake clock, which started earlier than "now".
            NotBefore = now.AddDays(-1),
            IssuedAt = now,
            Expires = now + (lifetime ?? TimeSpan.FromHours(4)),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(Rsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256)
        };
        lock (RsaGate)
        {
            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
    }

    public static string Admin(Guid? id = null) => Issue(ActorType.Administrator, id, mfa: true);

    public static string JobSeeker(Guid? id = null) => Issue(ActorType.JobSeeker, id);

    public static string Employer(Guid? id = null) => Issue(ActorType.Employer, id);

    public static string Partner(Guid? id = null) => Issue(ActorType.ExternalJobSite, id, clientId: "partner-test", scope: Scopes.PartnerApi);

    public static string Service() => Issue(ActorType.System, Guid.NewGuid(), clientId: "svc-test", scope: Scopes.Internal);
}

/// <summary>
/// Hosts the real API in-process with SQLite (shared in-memory), the in-memory cache and bus and a fake clock; only the infrastructure
/// providers differ from production. Derived factories set the connection string name and any BC-specific settings.
/// </summary>
public abstract class ApiTestFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly string _database = "test-" + Guid.NewGuid().ToString("N");

    protected abstract string ConnectionStringName { get; }

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public InMemoryEventBus Bus => Services.GetRequiredService<InMemoryEventBus>();

    protected virtual Dictionary<string, string?> Settings() => new()
    {
        ["Database:Provider"] = "Sqlite",
        [$"ConnectionStrings:{ConnectionStringName}"] = $"Data Source=file:{_database}?mode=memory&cache=shared",
        ["Cache:Provider"] = "InMemory",
        ["Cache:KeyPrefix"] = "test",
        ["Messaging:Provider"] = "InMemory",
        ["Outbox:Enabled"] = "false",
        ["Inbox:Enabled"] = "false",
        ["Jwt:Jwks"] = TestTokens.JwksJson,
        ["RateLimiting:Global:PermitLimit"] = "1000000",
        ["RateLimiting:Public:PermitLimit"] = "1000000",
        ["Swagger:Enabled"] = "true",
        ["Telemetry:Enabled"] = "false"
    };

    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings()));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            ConfigureTestServices(services);
        });
    }

    /// <summary>A client carrying the bearer token (or anonymous when null).</summary>
    public HttpClient ClientFor(string? token)
    {
        var client = CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    public async Task<int> PublishOutboxAsync<TContext>() where TContext : DbContext
    {
        var processor = Services.GetRequiredService<OutboxProcessor<TContext>>();
        var total = 0;
        int batch;
        while ((batch = await processor.ProcessBatchAsync(CancellationToken.None)) > 0)
        {
            total += batch;
        }

        return total;
    }

    public async Task<int> ProcessInboxAsync<TContext>() where TContext : DbContext
    {
        var processor = Services.GetRequiredService<InboxProcessor<TContext>>();
        var total = 0;
        int batch;
        while ((batch = await processor.ProcessBatchAsync(CancellationToken.None)) > 0)
        {
            total += batch;
        }

        return total;
    }

    public async Task<T> WithDbAsync<TContext, T>(Func<TContext, Task<T>> action) where TContext : DbContext
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<TContext>());
    }

    public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider);
    }
}

public static class HttpTestExtensions
{
    public static async Task<JsonNode> Json(this HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync()) ?? throw new InvalidOperationException("Empty body");

    public static async Task<string> Code(this HttpResponseMessage response) => (await response.Json())["code"]!.GetValue<string>();

    public static async Task ShouldBeProblemAsync(this HttpResponseMessage response, System.Net.HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Json();
        body["code"]!.GetValue<string>().Should().Be(code);
        body["status"]!.GetValue<int>().Should().Be((int)status);
        body["traceId"].Should().NotBeNull();
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object? body = null, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(body ?? new { }) };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return client.SendAsync(request);
    }
}

/// <summary>
/// Marks a test that needs real containers (Testcontainers). It is skipped automatically when Docker is not available,
/// so the suite stays green on machines without it. Always combine with [Trait("Category","Docker")].
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> DockerAvailable = new(Probe);

    public DockerFactAttribute()
    {
        if (!DockerAvailable.Value)
        {
            Skip = "Docker is not available on this machine (Testcontainers tests are skipped). Install Docker to run them.";
        }
    }

    private static bool Probe()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("DOCKER_TESTS"), "skip", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "version --format {{.Server.Version}}")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            });
            if (process is null || !process.WaitForExit(10_000))
            {
                process?.Kill();
                return false;
            }

            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(process.StandardOutput.ReadToEnd());
        }
        catch (Exception)
        {
            return false;
        }
    }
}
