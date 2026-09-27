using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using JobPlatform.AccountIdentity.Infrastructure.Delivery;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>
/// Hosts the real API in-process with SQLite (shared in-memory), the in-memory cache and bus, captured OTP delivery and a fake clock.
/// Everything except the infrastructure providers is the production composition root.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Adm1n!Passw0rd";
    public const string ServiceClientId = "svc-test";
    public const string ServiceClientSecret = "svc-test-secret";

    private readonly string _database = "identity-" + Guid.NewGuid().ToString("N");

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public InMemoryEventBus Bus => Services.GetRequiredService<InMemoryEventBus>();

    public CapturingMessageSender Messages => Services.GetRequiredService<CapturingMessageSender>();

    public CapturingLoggerProvider Logs { get; } = new();

    /// <summary>TOTP seed of the bootstrap administrator, captured when the tests enrol MFA.</summary>
    public string? AdminMfaSecret { get; set; }

    protected virtual Dictionary<string, string?> Settings() => new()
    {
        // Debug so the log-hygiene tests inspect everything the service can emit, including the dev-only debug lines.
        ["Serilog:MinimumLevel:Default"] = "Debug",
        ["Database:Provider"] = "Sqlite",
        ["ConnectionStrings:Identity"] = $"Data Source=file:{_database}?mode=memory&cache=shared",
        ["Cache:Provider"] = "InMemory",
        ["Cache:KeyPrefix"] = "test:account-identity",
        ["Messaging:Provider"] = "InMemory",
        ["Outbox:Enabled"] = "false",
        ["Delivery:Provider"] = "Capture",
        ["Security:MasterKey"] = "dGVzdC1tYXN0ZXIta2V5LTMyLWJ5dGVzLWxvbmchISE=",
        ["Security:Pepper"] = "test-pepper",
        ["Jwt:AccessTokenMinutes"] = "240",
        ["Security:Pbkdf2Iterations"] = "1000",
        ["Bootstrap:AdminEmail"] = AdminEmail,
        ["Bootstrap:AdminMobile"] = "+970590000001",
        ["Bootstrap:AdminPassword"] = AdminPassword,
        ["ServiceClients:Clients:0:ClientId"] = ServiceClientId,
        ["ServiceClients:Clients:0:Secret"] = ServiceClientSecret,
        ["RateLimiting:Auth:PermitLimit"] = "1000000",
        ["RateLimiting:Global:PermitLimit"] = "1000000",
        ["Swagger:Enabled"] = "true"
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings()));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<Serilog.Core.ILogEventSink>(Logs);
        });
    }

    /// <summary>Processes the outbox once, exactly like the background processor would (which is disabled in tests for determinism).</summary>
    public async Task<int> PublishOutboxAsync()
    {
        var processor = Services.GetRequiredService<OutboxProcessor<IdentityDbContext>>();
        var total = 0;
        int batch;
        while ((batch = await processor.ProcessBatchAsync(CancellationToken.None)) > 0)
        {
            total += batch;
        }

        return total;
    }

    public async Task<T> WithDbAsync<T>(Func<IdentityDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
    }

    private long _lastTotpStep = -1;

    public TotpCode Totp => new(this);

    /// <summary>
    /// Produces valid TOTP codes. The service rejects a replayed code (same or older time step), so when the current step was
    /// already handed out the fake clock moves to the start of the next step first - exactly what a real user waiting for a fresh code does.
    /// </summary>
    public sealed class TotpCode
    {
        private readonly ApiFactory _factory;

        public TotpCode(ApiFactory factory) => _factory = factory;

        public string For(string secret)
        {
            var clock = _factory.Clock;
            var step = clock.GetUtcNow().ToUnixTimeSeconds() / 30;
            if (step <= _factory._lastTotpStep)
            {
                clock.Advance(TimeSpan.FromSeconds(((_factory._lastTotpStep + 1) * 30) - clock.GetUtcNow().ToUnixTimeSeconds()));
                step = _factory._lastTotpStep + 1;
            }

            _factory._lastTotpStep = step;
            return TotpMfaService.ComputeCode(secret, clock.GetUtcNow().UtcDateTime);
        }

        /// <summary>The code for the current step without consuming it (used to assert that a replay is refused).</summary>
        public string Current(string secret) => TotpMfaService.ComputeCode(secret, _factory.Clock.GetUtcNow().UtcDateTime);
    }
}

/// <summary>Serilog sink that keeps every rendered event (message, properties, exception) so tests can assert no secret ever reaches the logs.</summary>
public sealed class CapturingLoggerProvider : Serilog.Core.ILogEventSink
{
    private readonly List<string> _lines = new();

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lines)
            {
                return _lines.ToArray();
            }
        }
    }

    public void Emit(Serilog.Events.LogEvent logEvent)
    {
        var properties = string.Join(' ', logEvent.Properties.Select(p => $"{p.Key}={p.Value}"));
        var line = $"{logEvent.RenderMessage()} {properties}{(logEvent.Exception is null ? string.Empty : " " + logEvent.Exception)}";
        lock (_lines)
        {
            _lines.Add(line);
        }
    }
}

/// <summary>Small typed client over the API for the scenarios the tests share.</summary>
public sealed class ApiClient
{
    private static int _counter = 5_000_000;

    public ApiClient(ApiFactory factory, HttpClient? http = null)
    {
        Factory = factory;
        Http = http ?? factory.CreateClient();
        DeviceId = Guid.NewGuid().ToString("N");
        Http.DefaultRequestHeaders.Add("X-Device-Id", DeviceId);
    }

    public ApiFactory Factory { get; }
    public HttpClient Http { get; }
    public string DeviceId { get; }

    public static string NewMobile() => "+9705" + Interlocked.Increment(ref _counter).ToString("D8");

    public static string NewEmail() => $"user{Guid.NewGuid():N}@example.com";

    public ApiClient Anonymous() => new(Factory);

    public ApiClient WithLanguage(string language)
    {
        Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        Http.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        return this;
    }

    public ApiClient Bearer(string token)
    {
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return this;
    }

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return Http.SendAsync(request);
    }

    public Task<HttpResponseMessage> PutAsync(string url, object? body = null) => Http.PutAsJsonAsync(url, body ?? new { });

    // ---------------------------------------------------------------- scenario helpers

    public async Task<(Guid Id, string Mobile, string Email)> RegisterJobSeekerAsync(string? mobile = null, string? email = null, string password = "Str0ngPass")
    {
        mobile ??= NewMobile();
        email ??= NewEmail();
        var response = await PostAsync("/api/v1/accounts/job-seekers", new { fullName = "Test Seeker", mobile, email, password });
        response.EnsureSuccessStatusCode();
        return ((await response.Json())["accountId"]!.GetValue<Guid>(), mobile, email);
    }

    public string LastCode(string mobile) => Factory.Messages.LastFor("activation-code", mobile)!.Code!;

    public async Task<Guid> ActiveJobSeekerAsync(string? mobile = null, string? email = null, string password = "Str0ngPass")
    {
        var (id, m, _) = await RegisterJobSeekerAsync(mobile, email, password);
        (await PostAsync($"/api/v1/accounts/{id}/activate", new { code = LastCode(m) })).EnsureSuccessStatusCode();
        return id;
    }

    public async Task<JsonNode> LoginAsync(string username, string password, string mechanism = "password", string? mfaCode = null) =>
        await (await PostAsync("/api/v1/auth/login", new { username, password, mechanism, mfaCode })).Json();

    public async Task<string> AccessTokenAsync(string username, string password)
    {
        var result = await LoginAsync(username, password);
        return result["tokens"]!["accessToken"]!.GetValue<string>();
    }

    public async Task<(string AccessToken, string RefreshToken)> TokensAsync(string username, string password)
    {
        var result = await LoginAsync(username, password);
        return (result["tokens"]!["accessToken"]!.GetValue<string>(), result["tokens"]!["refreshToken"]!.GetValue<string>());
    }

    /// <summary>Signs the bootstrap administrator in through the full MFA flow (enrol on first use, then TOTP).</summary>
    public async Task<string> AdminTokenAsync()
    {
        var login = await LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        var mfaToken = login["mfaToken"]!.GetValue<string>();
        if (login["status"]!.GetValue<string>() == "MfaEnrollmentRequired")
        {
            var enroll = await (await PostAsync("/api/v1/auth/mfa/enroll", new { mfaToken })).Json();
            Factory.AdminMfaSecret = enroll["secret"]!.GetValue<string>();
        }

        var verified = await (await PostAsync("/api/v1/auth/mfa/verify", new { mfaToken, code = Factory.Totp.For(Factory.AdminMfaSecret!) })).Json();
        return verified["tokens"]!["accessToken"]!.GetValue<string>();
    }

    public async Task<ApiClient> AdminAsync()
    {
        var client = new ApiClient(Factory);
        client.Bearer(await client.AdminTokenAsync());
        return client;
    }

    public async Task<ApiClient> UserAsync(string username, string password)
    {
        var client = new ApiClient(Factory);
        client.Bearer(await client.AccessTokenAsync(username, password));
        return client;
    }

    public async Task<ApiClient> ServiceAsync()
    {
        var client = new ApiClient(Factory);
        var response = await client.Http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = ApiFactory.ServiceClientId, ["client_secret"] = ApiFactory.ServiceClientSecret
        }));
        response.EnsureSuccessStatusCode();
        client.Bearer((await response.Json())["access_token"]!.GetValue<string>());
        return client;
    }

    /// <summary>Registers a partner, approves it as the administrator and returns its client plus account id.</summary>
    public async Task<(ApiClient Partner, Guid AccountId, string Email)> ActivePartnerAsync(string password = "Str0ngPass")
    {
        var email = NewEmail();
        var register = await PostAsync("/api/v1/accounts/external-job-sites", new
        {
            organisationName = "Partner Ltd", contactEmail = email, mobile = NewMobile(), identity = "partner-" + Guid.NewGuid().ToString("N"), password
        });
        register.EnsureSuccessStatusCode();
        var id = (await register.Json())["accountId"]!.GetValue<Guid>();
        var admin = await AdminAsync();
        (await admin.PostAsync($"/api/v1/accounts/{id}/approve-partner")).EnsureSuccessStatusCode();
        return (await UserAsync(email, password), id, email);
    }
}

public static class HttpExtensions
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
}
