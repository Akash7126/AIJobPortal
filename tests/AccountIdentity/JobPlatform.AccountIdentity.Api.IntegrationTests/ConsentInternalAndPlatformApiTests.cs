using System.Net;
using System.Text.Json.Nodes;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>Cookie / privacy consent (US-4.1-04).</summary>
public class ConsentApiTests : IClassFixture<ApiFactory>
{
    private const string Version = "2026-01";
    private readonly ApiFactory _factory;

    public ConsentApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-01")]
    public async Task FirstVisit_ShowsTheBannerAndLinksToThePrivacyPolicy()
    {
        var response = await new ApiClient(_factory).Http.GetAsync("/api/v1/consents/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["bannerRequired"]!.GetValue<bool>().Should().BeTrue();
        body["privacyPolicyUrl"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        body["currentPolicyVersion"]!.GetValue<string>().Should().Be(Version);
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-04")]
    public async Task BeforeAnyChoice_NonEssentialCollectionIsWithheld()
    {
        var body = await (await new ApiClient(_factory).Http.GetAsync($"/api/v1/consents/current?guestId={Guid.NewGuid()}")).Json();

        var allowed = body["allowed"]!;
        allowed["necessary"]!.GetValue<bool>().Should().BeTrue();
        allowed["analytics"]!.GetValue<bool>().Should().BeFalse();
        allowed["preferences"]!.GetValue<bool>().Should().BeFalse();
        allowed["marketing"]!.GetValue<bool>().Should().BeFalse();
        body["bannerRequired"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-03")]
    public async Task Banner_IsProvidedInArabicAndEnglish()
    {
        var text = (await (await new ApiClient(_factory).Http.GetAsync("/api/v1/consents/current")).Json())["bannerText"]!;

        text["en"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        text["ar"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace().And.MatchRegex("\\p{IsArabic}");
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-02")]
    public async Task ConsentRecord_IsAuditable_VersionedTimestampedAndHoldsNoPersonalData()
    {
        // Whether the policy text satisfies GDPR / Palestinian data-protection law is a legal review (README). The technical controls
        // the platform owes that review are verified here: the decision is tied to a policy version, timestamped, and anonymous.
        var guest = Guid.NewGuid();
        (await new ApiClient(_factory).PostAsync("/api/v1/consents", new { guestId = guest, policyVersion = Version, analytics = true, preferences = false, marketing = false, locale = "ar" }))
            .EnsureSuccessStatusCode();

        var row = await _factory.WithDbAsync(db => db.PrivacyConsents.AsNoTracking().SingleAsync(c => c.GuestId == guest));

        row.PolicyVersion.Should().Be(Version);
        row.DecidedAtUtc.Should().BeCloseTo(_factory.Clock.GetUtcNow().UtcDateTime, TimeSpan.FromSeconds(1));
        row.Locale.Should().Be(JobPlatform.SharedKernel.Common.Enums.Language.Ar);
        row.Choices.Necessary.Should().BeTrue();
        var columns = await _factory.WithDbAsync(db => Task.FromResult(db.Model.FindEntityType(typeof(JobPlatform.AccountIdentity.Domain.Consent.PrivacyConsent))!
            .GetProperties().Select(p => p.Name).ToList()));
        columns.Should().NotContain(new[] { "Email", "Mobile", "IpAddress", "Name" });
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-05")]
    public async Task RecordingTwice_ForTheSameGuestAndVersion_UpdatesTheRecordAndNeverDuplicatesIt()
    {
        var client = new ApiClient(_factory);
        var guest = Guid.NewGuid();
        object Body(bool analytics) => new { guestId = guest, policyVersion = Version, analytics, preferences = false, marketing = false, locale = "en" };

        (await client.PostAsync("/api/v1/consents", Body(false))).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/v1/consents", Body(false))).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/v1/consents", Body(true))).EnsureSuccessStatusCode();

        var rows = await _factory.WithDbAsync(db => db.PrivacyConsents.AsNoTracking().Where(c => c.GuestId == guest).ToListAsync());
        rows.Should().ContainSingle("UQ(GuestId, PolicyVersion): no duplicate consent record");
        rows.Single().Choices.Analytics.Should().BeTrue();
        var status = await (await client.Http.GetAsync($"/api/v1/consents/current?guestId={guest}")).Json();
        status["bannerRequired"]!.GetValue<bool>().Should().BeFalse("the banner is not shown again");
        status["allowed"]!["analytics"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-05")]
    public async Task Record_WithoutAGuestId_MintsOne_AndTheSameKeyReplays()
    {
        var client = new ApiClient(_factory);
        var key = Guid.NewGuid().ToString();
        var body = new { policyVersion = Version, analytics = false, preferences = false, marketing = false, locale = "en" };

        var first = await (await client.PostAsync("/api/v1/consents", body, key)).Json();
        var replay = await (await client.PostAsync("/api/v1/consents", body, key)).Json();

        first["guestId"]!.GetValue<Guid>().Should().NotBeEmpty();
        replay["guestId"]!.GetValue<Guid>().Should().Be(first["guestId"]!.GetValue<Guid>());
        first["bannerRequired"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-05")]
    public async Task Record_ForAnOutdatedPolicyVersionOrBadLocale_Returns400()
    {
        var client = new ApiClient(_factory);

        var stale = await client.PostAsync("/api/v1/consents", new { policyVersion = "2020-01", analytics = true });
        await stale.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await stale.Json())["errors"]!["policyVersion"]!.AsArray().Select(e => e!.GetValue<string>()).Should().Contain("VAL.PolicyVersion.NotCurrent");
        var locale = await client.PostAsync("/api/v1/consents", new { policyVersion = Version, locale = "xx" });
        (await locale.Json())["errors"]!["locale"].Should().NotBeNull();
    }
}

/// <summary>Internal service-to-service endpoints (/internal/v1) and JWKS.</summary>
public class InternalApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InternalApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Jwks_PublishesThePublicKey_SoASecondHostCanValidateTokensLocally()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);
        var token = await client.AccessTokenAsync(mobile, "Str0ngPass");

        var jwksResponse = await client.Http.GetAsync("/.well-known/jwks.json");

        jwksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        jwksResponse.Headers.CacheControl!.Public.Should().BeTrue();
        var jwks = await jwksResponse.Json();
        var keyJson = jwks["keys"]!.AsArray().First()!;
        keyJson["kty"]!.GetValue<string>().Should().Be("RSA");
        keyJson["alg"]!.GetValue<string>().Should().Be("RS256");
        keyJson.ToJsonString().Should().NotContainEquivalentOf("\"d\"").And.NotContainEquivalentOf("\"p\"", "no private key material is ever published");

        // "Second test host": a validator that knows only the issuer, audience and the JWKS document - exactly what another BC has.
        var keySet = new JsonWebKeySet(jwks.ToJsonString());
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "https://identity.jobplatform.local",
            ValidAudience = "jobplatform",
            IssuerSigningKeys = keySet.GetSigningKeys(),
            ValidateLifetime = true,
            LifetimeValidator = (_, _, _, _) => true
        });
        result.IsValid.Should().BeTrue(result.Exception?.Message);
        result.Claims["actor_type"].Should().Be("JobSeeker");
        result.Claims.Should().ContainKey("role_id").And.ContainKey("sid").And.ContainKey("sub");
        result.Claims.Should().NotContainKey("permissions", "tokens carry role ids only (D-03)");

        var tampered = token[..^4] + (token[^4] == 'A' ? "BBB" : "AAA") + token[^1];
        (await new JsonWebTokenHandler().ValidateTokenAsync(tampered, new TokenValidationParameters
        {
            ValidIssuer = "https://identity.jobplatform.local", ValidAudience = "jobplatform", IssuerSigningKeys = keySet.GetSigningKeys(), ValidateLifetime = false
        })).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SigningKeys_RotateAheadOfExpiry_AndOldKeysStayPublishedForValidation()
    {
        var keys = _factory.Services.GetRequiredService<SigningKeyService>();
        var before = (await (await new ApiClient(_factory).Http.GetAsync("/.well-known/jwks.json")).Json())["keys"]!.AsArray().Count;

        _factory.Clock.Advance(TimeSpan.FromDays(80));
        await keys.EnsureActiveKeyAsync();
        var after = (await (await new ApiClient(_factory).Http.GetAsync("/.well-known/jwks.json")).Json())["keys"]!.AsArray();

        after.Count.Should().Be(before + 1);
        after.Select(k => k!["kid"]!.GetValue<string>()).Distinct().Should().HaveCount(after.Count);
    }

    [Fact]
    public async Task GetAccountSummary_ForServices_ReturnsActorTypeAndStanding()
    {
        var client = new ApiClient(_factory);
        var id = await client.ActiveJobSeekerAsync();
        var service = await client.ServiceAsync();

        var response = await service.Http.GetAsync($"/internal/v1/accounts/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["actorType"]!.GetValue<string>().Should().Be("JobSeeker");
        body["standing"]!.GetValue<string>().Should().Be("Active");
        (await response.Content.ReadAsStringAsync()).Should().NotContain("mobile").And.NotContain("email", "the summary carries no PII");
        await (await service.Http.GetAsync($"/internal/v1/accounts/{Guid.NewGuid()}")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-ACCOUNT-NOT-FOUND");
    }

    [Fact]
    public async Task InternalEndpoints_RequireAServiceToken()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var id = await client.ActiveJobSeekerAsync(mobile);
        var user = await client.UserAsync(mobile, "Str0ngPass");
        var admin = await client.AdminAsync();

        (await client.Anonymous().Http.GetAsync($"/internal/v1/accounts/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await user.Http.GetAsync($"/internal/v1/accounts/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Http.GetAsync($"/internal/v1/accounts/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var wrongSecret = await client.Http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = ApiFactory.ServiceClientId, ["client_secret"] = "nope"
        }));
        await wrongSecret.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-APIF-INVALID-CLIENT");
    }

    [Fact]
    public async Task DeactivationRequest_FromBc04_DeactivatesTheAccountAndPublishesAccountSuspended()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var id = await client.ActiveJobSeekerAsync(mobile);
        var (access, _) = await client.TokensAsync(mobile, "Str0ngPass");
        var service = await client.ServiceAsync();

        var response = await service.PostAsync($"/internal/v1/accounts/{id}/deactivation-requests", new { kind = "Delete", reason = "privacy setting: delete my data" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await response.Json())["standing"]!.GetValue<string>().Should().Be("DeletionRequested");
        var suspended = await _factory.PublishedEventAsync("AccountSuspended", id);
        suspended["standing"]!.GetValue<string>().Should().Be("DeletionRequested");
        suspended["reason"]!.GetValue<string>().Should().Contain("privacy setting");
        suspended["actorId"]!.GetValue<Guid>().Should().Be(id, "the owner asked for it");
        await (await client.Anonymous().Bearer(access).PostAsync("/api/v1/auth/logout")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
        await (await service.PostAsync($"/internal/v1/accounts/{id}/deactivation-requests", new { kind = "Delete", reason = "again" }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-AUM-STATE-CONFLICT");
        await (await service.PostAsync($"/internal/v1/accounts/{Guid.NewGuid()}/deactivation-requests", new { kind = "Delete", reason = "x" }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-ACCOUNT-NOT-FOUND");
        (await service.PostAsync($"/internal/v1/accounts/{id}/deactivation-requests", new { kind = "Explode", reason = "x" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CheckPermission_ReportsWhetherTheAccountsRolesGrantIt()
    {
        var client = new ApiClient(_factory);
        var id = await client.ActiveJobSeekerAsync();
        var service = await client.ServiceAsync();

        var yes = await (await service.PostAsync($"/internal/v1/accounts/{id}/check-permission", new { permission = "profile.manage" })).Json();
        var no = await (await service.PostAsync($"/internal/v1/accounts/{id}/check-permission", new { permission = "accounts.ban" })).Json();

        yes["allowed"]!.GetValue<bool>().Should().BeTrue();
        no["allowed"]!.GetValue<bool>().Should().BeFalse();
    }
}

/// <summary>Cross-cutting HTTP behaviour: correlation, problem details, OpenAPI, health, rate limiting, log hygiene.</summary>
public class PlatformApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PlatformApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CorrelationId_IsEchoedAndPropagatedToTheOutboxMessage()
    {
        var client = new ApiClient(_factory);
        var correlation = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/accounts/job-seekers")
        {
            Content = JsonContent(new { fullName = "C", mobile = ApiClient.NewMobile(), password = "Str0ngPass" })
        };
        request.Headers.Add("X-Correlation-Id", correlation.ToString());

        var response = await client.Http.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().Should().Be(correlation.ToString());
        var id = (await response.Json())["accountId"]!.GetValue<Guid>();
        (await _factory.OutboxAsync("AccountCreated", id.ToString())).Single().PayloadOf()["correlationId"]!.GetValue<Guid>().Should().Be(correlation);
        var generated = await client.Http.GetAsync("/health/live");
        Guid.TryParse(generated.Headers.GetValues("X-Correlation-Id").Single(), out _).Should().BeTrue();
    }

    private static StringContent JsonContent(object body) =>
        new(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task ProblemDetails_HaveTheDocumentedShape()
    {
        var response = await new ApiClient(_factory).PostAsync("/api/v1/auth/login", new { username = "x@y.com", password = "p", mechanism = "password" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-INVALID-CREDENTIALS");
        var body = await response.Json();
        body["type"]!.GetValue<string>().Should().Contain("E-AAFR-INVALID-CREDENTIALS");
        body["title"]!.GetValue<string>().Should().Be("Unauthorized");
        body["detail"]!.GetValue<string>().Should().NotBeNullOrEmpty();
        body["instance"]!.GetValue<string>().Should().Be("/api/v1/auth/login");
        body["correlationId"].Should().NotBeNull();
    }

    [Fact]
    public async Task OpenApiDocument_IsPublishedWithTheMainRoutes()
    {
        var response = await new ApiClient(_factory).Http.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var paths = (await response.Json())["paths"]!.AsObject().Select(p => p.Key).ToList();
        paths.Should().Contain(new[]
        {
            "/api/v1/accounts/job-seekers", "/api/v1/accounts/employers", "/api/v1/accounts/external-job-sites", "/api/v1/accounts/{id}/activate",
            "/api/v1/auth/login", "/api/v1/auth/refresh", "/api/v1/auth/logout", "/api/v1/auth/password", "/oauth/token", "/api/v1/api-credentials",
            "/api/v1/consents", "/.well-known/jwks.json", "/internal/v1/accounts/{id}", "/api/v1/admin/roles"
        });
        (await new ApiClient(_factory).Http.GetAsync("/swagger/index.html")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegistrationFlow_NeverLogsPasswordsOtpsOrTokens()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        const string password = "L0gHygienePw!";
        var (id, _, _) = await client.RegisterJobSeekerAsync(mobile, password: password);
        var code = client.LastCode(mobile);
        await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code });
        var (access, refresh) = await client.TokensAsync(mobile, password);
        await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Wr0ngPassword!", mechanism = "password" });

        var lines = _factory.Logs.Lines;

        lines.Should().NotBeEmpty();
        lines.Should().NotContain(l => l.Contains(password) || l.Contains("Wr0ngPassword!") || l.Contains(code) || l.Contains(access) || l.Contains(refresh));
        lines.Should().NotContain(l => l.Contains("$pbkdf2"));
    }

    [Fact]
    public async Task PendingDomainEvents_AreWrittenAtomically_OutboxRowsExistOnlyForCommittedChanges()
    {
        var client = new ApiClient(_factory);
        var before = (await _factory.OutboxAsync()).Count;

        var ok = await client.RegisterJobSeekerAsync();
        var afterOk = await _factory.OutboxAsync();
        (await client.PostAsync("/api/v1/accounts/job-seekers", new { fullName = "Dup", mobile = ok.Mobile, password = "Str0ngPass" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var afterRejected = await _factory.OutboxAsync();

        afterOk.Count.Should().Be(before + 1);
        afterRejected.Count.Should().Be(afterOk.Count);
    }
}

/// <summary>Outbox processor behaviour (retry, backoff, dead-letter) driven deterministically.</summary>
public class OutboxDeliveryApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OutboxDeliveryApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task BrokerFailure_KeepsTheMessagePending_WithBackoff_AndItIsDeliveredOnceTheBrokerRecovers()
    {
        var client = new ApiClient(_factory);
        var (id, _, _) = await client.RegisterJobSeekerAsync();
        await _factory.PublishOutboxAsync();
        _factory.Bus.Clear();
        var (id2, _, _) = await client.RegisterJobSeekerAsync();

        _factory.Bus.FailWith = new InvalidOperationException("broker down");
        await _factory.PublishOutboxAsync();
        var failed = (await _factory.OutboxAsync("AccountCreated", id2.ToString())).Single();

        failed.Status.Should().Be(OutboxStatus.Pending);
        failed.Attempts.Should().Be(1);
        failed.LastError.Should().Contain("broker down");
        failed.NextAttemptUtc.Should().BeAfter(_factory.Clock.GetUtcNow().UtcDateTime);
        _factory.Bus.Messages.Should().BeEmpty();

        _factory.Bus.FailWith = null;
        await _factory.PublishOutboxAsync();
        (await _factory.OutboxAsync("AccountCreated", id2.ToString())).Single().Status.Should().Be(OutboxStatus.Pending, "backoff has not elapsed yet");

        _factory.Clock.Advance(TimeSpan.FromMinutes(6));
        await _factory.PublishOutboxAsync();
        var delivered = (await _factory.OutboxAsync("AccountCreated", id2.ToString())).Single();
        delivered.Status.Should().Be(OutboxStatus.Published);
        delivered.ProcessedOnUtc.Should().NotBeNull();
        _factory.Bus.Messages.Should().ContainSingle(m => m.MessageId == delivered.Id);
        id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PersistentFailure_EndsInDeadLetteredAfterMaxAttempts()
    {
        var client = new ApiClient(_factory);
        var (id, _, _) = await client.RegisterJobSeekerAsync();
        await _factory.PublishOutboxAsync();
        var (poisonId, _, _) = await client.RegisterJobSeekerAsync();

        _factory.Bus.FailWith = new InvalidOperationException("permanent failure");
        try
        {
            for (var i = 0; i < 10; i++)
            {
                await _factory.PublishOutboxAsync();
                _factory.Clock.Advance(TimeSpan.FromMinutes(6));
            }
        }
        finally
        {
            _factory.Bus.FailWith = null;
        }

        var row = (await _factory.OutboxAsync("AccountCreated", poisonId.ToString())).Single();
        row.Status.Should().Be(OutboxStatus.DeadLettered);
        row.Attempts.Should().Be(10);
        await _factory.PublishOutboxAsync();
        _factory.Bus.Messages.Should().NotContain(m => m.MessageId == row.Id, "dead-lettered rows are not retried");
        id.Should().NotBeEmpty();
    }
}

/// <summary>ASP.NET rate limiter (THR-055): a stricter budget on the authentication surface.</summary>
public class RateLimitedApiFactory : ApiFactory
{
    protected override Dictionary<string, string?> Settings()
    {
        var settings = base.Settings();
        settings["RateLimiting:Auth:PermitLimit"] = "3";
        return settings;
    }
}

public class HttpRateLimitingTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _factory;

    public HttpRateLimitingTests(RateLimitedApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AuthEndpoints_ReturnProblem429_WhenTheBudgetIsExhausted()
    {
        var client = new ApiClient(_factory);
        var body = new { username = "x@y.com", password = "p", mechanism = "password" };

        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsync("/api/v1/auth/login", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var limited = await client.PostAsync("/api/v1/auth/login", body);

        await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-AAFR-RATE-LIMITED");
        limited.Headers.RetryAfter.Should().NotBeNull();
        (await new ApiClient(_factory).Http.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK, "only the auth surface is throttled this hard");
        (await new ApiClient(_factory).PostAsync("/api/v1/auth/login", body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a different source has its own budget");
    }
}
