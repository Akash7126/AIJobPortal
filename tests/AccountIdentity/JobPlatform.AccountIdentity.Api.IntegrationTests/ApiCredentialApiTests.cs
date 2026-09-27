using System.Net;
using System.Security.Claims;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

public class ApiCredentialApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiCredentialApiTests(ApiFactory factory) => _factory = factory;

    private static Task<HttpResponseMessage> TokenAsync(ApiClient client, string clientId, string clientSecret, string grant = "client_credentials") =>
        client.Http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = grant, ["client_id"] = clientId, ["client_secret"] = clientSecret
        }));

    private async Task<(ApiClient Partner, Guid PartnerId, System.Text.Json.Nodes.JsonNode Credential)> IssuedAsync(object? request = null)
    {
        var (partner, partnerId, _) = await new ApiClient(_factory).ActivePartnerAsync();
        var response = await partner.PostAsync("/api/v1/api-credentials", request ?? new { });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (partner, partnerId, await response.Json());
    }

    // ------------------------------------------------------------------ US-3.1.3-02

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public async Task Issue_ForAnActivePartner_ReturnsAUniqueKeyAndSecretWithDefaultLimitsAndExpiration()
    {
        var (_, _, credential) = await IssuedAsync();
        var (_, _, second) = await IssuedAsync();

        credential["clientId"]!.GetValue<string>().Should().StartWith("jp_");
        credential["clientSecret"]!.GetValue<string>().Should().StartWith("jps_").And.HaveLength(4 + 43);
        credential["clientId"]!.GetValue<string>().Should().NotBe(second["clientId"]!.GetValue<string>());
        credential["maxRequests"]!.GetValue<int>().Should().Be(1000);
        credential["periodSeconds"]!.GetValue<int>().Should().Be(3600);
        credential["ipWhitelist"]!.AsArray().Should().BeEmpty();
        (credential["expiresAtUtc"]!.GetValue<DateTime>() - _factory.Clock.GetUtcNow().UtcDateTime).TotalDays.Should().BeApproximately(365, 1);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public async Task Issue_ForNonPartnersAndAnonymous_IsRefused()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);
        var seeker = await client.UserAsync(mobile, "Str0ngPass");

        await (await seeker.PostAsync("/api/v1/api-credentials", new { })).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AAFR-FORBIDDEN");
        (await client.Anonymous().PostAsync("/api/v1/api-credentials", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public async Task Issue_ForAPartnerWhoIsNotYetActive_CannotEvenSignIn()
    {
        var client = new ApiClient(_factory);
        var email = ApiClient.NewEmail();
        await client.PostAsync("/api/v1/accounts/external-job-sites", new
        {
            organisationName = "Pending Partner", contactEmail = email, mobile = ApiClient.NewMobile(), identity = "p-" + Guid.NewGuid().ToString("N"), password = "Str0ngPass"
        });

        var login = await client.PostAsync("/api/v1/auth/login", new { username = email, password = "Str0ngPass", mechanism = "password" });

        await login.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AAFR-ACCOUNT-PENDING");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-02")]
    public async Task Issue_IpWhitelistLimitsAndExpirationAreEachIndependentlyConfigurable()
    {
        var expiry = _factory.Clock.GetUtcNow().UtcDateTime.AddDays(30);

        var (_, _, onlyIp) = await IssuedAsync(new { ipWhitelist = new[] { "203.0.113.0/24", "198.51.100.7" } });
        onlyIp["ipWhitelist"]!.AsArray().Select(i => i!.GetValue<string>()).Should().Equal("203.0.113.0/24", "198.51.100.7");
        onlyIp["maxRequests"]!.GetValue<int>().Should().Be(1000, "limits were not configured, so the default applies");

        var (_, _, onlyLimits) = await IssuedAsync(new { maxRequests = 50, periodSeconds = 60 });
        onlyLimits["maxRequests"]!.GetValue<int>().Should().Be(50);
        onlyLimits["periodSeconds"]!.GetValue<int>().Should().Be(60);
        onlyLimits["ipWhitelist"]!.AsArray().Should().BeEmpty();

        var (_, _, onlyExpiry) = await IssuedAsync(new { expiresAtUtc = expiry });
        onlyExpiry["expiresAtUtc"]!.GetValue<DateTime>().Should().BeCloseTo(expiry, TimeSpan.FromSeconds(1));
        onlyExpiry["maxRequests"]!.GetValue<int>().Should().Be(1000);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-02")]
    public async Task Issue_InvalidControls_ReturnFieldErrors()
    {
        var (partner, _, _) = await new ApiClient(_factory).ActivePartnerAsync();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;

        var badIp = await partner.PostAsync("/api/v1/api-credentials", new { ipWhitelist = new[] { "999.1.1.1" } });
        await badIp.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await badIp.Json())["errors"]!.AsObject().Select(e => e.Key).Should().Contain(k => k.StartsWith("ipWhitelist"));

        var badLimit = await partner.PostAsync("/api/v1/api-credentials", new { maxRequests = 0 });
        (await badLimit.Json())["errors"]!.AsObject().Select(e => e.Key).Should().Contain("maxRequests");

        var past = await partner.PostAsync("/api/v1/api-credentials", new { expiresAtUtc = now.AddDays(-1) });
        (await past.Json())["errors"]!.AsObject().Select(e => e.Key).Should().Contain("expiresAtUtc");
        var tooFar = await partner.PostAsync("/api/v1/api-credentials", new { expiresAtUtc = now.AddYears(5) });
        (await tooFar.Json())["errors"]!.AsObject().Select(e => e.Key).Should().Contain("expiresAtUtc");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-03")]
    public async Task Issue_WhenACredentialIsAlreadyActive_RevokesThePreviousOneFirst()
    {
        var (partner, partnerId, first) = await IssuedAsync();

        var secondResponse = await partner.PostAsync("/api/v1/api-credentials", new { });
        var second = await secondResponse.Json();

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        second["revokedPreviousCredentialId"]!.GetValue<Guid>().Should().Be(first["apiCredentialId"]!.GetValue<Guid>());
        var anonymous = new ApiClient(_factory);
        await (await TokenAsync(anonymous, first["clientId"]!.GetValue<string>(), first["clientSecret"]!.GetValue<string>()))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-TPJPRI-REVOKED");
        (await TokenAsync(anonymous, second["clientId"]!.GetValue<string>(), second["clientSecret"]!.GetValue<string>())).StatusCode.Should().Be(HttpStatusCode.OK);
        var active = await _factory.WithDbAsync(db => db.ApiCredentials.AsNoTracking().Where(c => c.Status == ApiCredentialStatus.Active).ToListAsync());
        active.Count(c => c.PartnerAccountId.Value == partnerId).Should().Be(1, "INV-13: one active credential per partner");
        var current = await (await partner.Http.GetAsync("/api/v1/api-credentials/current")).Json();
        current["apiCredentialId"]!.GetValue<Guid>().Should().Be(second["apiCredentialId"]!.GetValue<Guid>());
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-03")]
    public async Task TheSecret_IsShownExactlyOnce_AndOnlyItsHashIsStored()
    {
        var (partner, _, credential) = await IssuedAsync();
        var secret = credential["clientSecret"]!.GetValue<string>();

        var current = await partner.Http.GetAsync("/api/v1/api-credentials/current");
        var body = await current.Content.ReadAsStringAsync();

        body.Should().NotContain(secret).And.NotContain("clientSecret").And.NotContain("secretHash");
        var stored = await _factory.WithDbAsync(db => db.ApiCredentials.AsNoTracking().ToListAsync());
        stored.Should().OnlyContain(c => c.SecretHash != secret);
        stored.Single(c => c.Id.Value == credential["apiCredentialId"]!.GetValue<Guid>()).SecretHash.Should().NotBeNullOrEmpty();
        _factory.Logs.Lines.Should().NotContain(l => l.Contains(secret));
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-04")]
    public async Task ExpiredCredential_IsRejectedAtTheTokenEndpointWithTpjpriExpired()
    {
        var (_, _, credential) = await IssuedAsync(new { expiresAtUtc = _factory.Clock.GetUtcNow().UtcDateTime.AddHours(1) });
        var id = credential["clientId"]!.GetValue<string>();
        var secret = credential["clientSecret"]!.GetValue<string>();
        (await TokenAsync(new ApiClient(_factory), id, secret)).StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Clock.Advance(TimeSpan.FromHours(2));
        var expired = await TokenAsync(new ApiClient(_factory), id, secret);

        await expired.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-TPJPRI-EXPIRED");
        var status = await _factory.WithDbAsync(db => db.ApiCredentials.AsNoTracking().Where(c => c.KeyId == id).Select(c => c.Status).SingleAsync());
        status.Should().Be(ApiCredentialStatus.Expired, "the failed attempt is persisted");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-02")]
    public async Task Revoke_ByTheOwner_Returns204AndTheCredentialStopsWorking_OthersCannotRevokeIt()
    {
        var (partner, _, credential) = await IssuedAsync();
        var id = credential["apiCredentialId"]!.GetValue<Guid>();
        var (otherPartner, _, _) = await new ApiClient(_factory).ActivePartnerAsync();

        await (await otherPartner.PostAsync($"/api/v1/api-credentials/{id}/revoke")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-API-CREDENTIAL-NOT-FOUND");
        (await partner.PostAsync($"/api/v1/api-credentials/{id}/revoke")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await (await TokenAsync(new ApiClient(_factory), credential["clientId"]!.GetValue<string>(), credential["clientSecret"]!.GetValue<string>()))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-TPJPRI-REVOKED");
        await (await partner.PostAsync($"/api/v1/api-credentials/{id}/revoke")).ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-AUM-STATE-CONFLICT");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public async Task ApiCredentialCreated_IsPublishedWithExpiry_ButNeverWithTheSecretOrItsHash()
    {
        var (_, partnerId, credential) = await IssuedAsync();
        var credentialId = credential["apiCredentialId"]!.GetValue<Guid>();

        var published = await _factory.PublishedEventAsync("ApiCredentialCreated", credentialId);

        published["apiCredentialId"]!.GetValue<Guid>().Should().Be(credentialId);
        published["accountId"]!.GetValue<Guid>().Should().Be(partnerId);
        published["actorId"]!.GetValue<Guid>().Should().Be(partnerId);
        published["expiresAtUtc"]!.GetValue<DateTime>().Should().Be(credential["expiresAtUtc"]!.GetValue<DateTime>());
        var raw = published.ToJsonString();
        raw.Should().NotContain(credential["clientSecret"]!.GetValue<string>()).And.NotContainEquivalentOf("secret").And.NotContainEquivalentOf("hash");
        var row = (await _factory.OutboxAsync("ApiCredentialCreated", credentialId.ToString())).Single();
        row.RoutingKey.Should().Be("api-credential.created.v1");
        var hash = await _factory.WithDbAsync(db => db.ApiCredentials.AsNoTracking().Where(c => c.Id == new JobPlatform.AccountIdentity.Domain.Common.ApiCredentialId(credentialId)).Select(c => c.SecretHash).SingleAsync());
        row.Payload.Should().NotContain(hash);
    }

    // ------------------------------------------------------------------ US-3.4.3-04

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-01")]
    public async Task ClientCredentials_ValidClient_ReceivesATokenThatAuthenticatesApiCalls()
    {
        var (_, partnerId, credential) = await IssuedAsync();

        var response = await TokenAsync(new ApiClient(_factory), credential["clientId"]!.GetValue<string>(), credential["clientSecret"]!.GetValue<string>());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = await response.Json();
        body["token_type"]!.GetValue<string>().Should().Be("Bearer");
        body["scope"]!.GetValue<string>().Should().Be("partner.api");
        body["expires_in"]!.GetValue<int>().Should().BeInRange(1, 3600);
        var api = new ApiClient(_factory).Bearer(body["access_token"]!.GetValue<string>());
        var call = await api.Http.GetAsync("/api/v1/api-credentials/current");
        call.StatusCode.Should().Be(HttpStatusCode.OK, "the client token authenticates and the call is processed");
        (await call.Json())["keyId"]!.GetValue<string>().Should().Be(credential["clientId"]!.GetValue<string>());
        partnerId.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-01")]
    public async Task ClientCredentials_AcceptsJsonBodiesToo_AndRejectsUnsupportedGrantsAndBadCredentials()
    {
        var (_, _, credential) = await IssuedAsync();
        var client = new ApiClient(_factory);

        var json = await client.PostAsync("/oauth/token", new { grant_type = "client_credentials", client_id = credential["clientId"]!.GetValue<string>(), client_secret = credential["clientSecret"]!.GetValue<string>() });
        json.StatusCode.Should().Be(HttpStatusCode.OK);

        var wrongGrant = await TokenAsync(client, "x", "y", grant: "password");
        await wrongGrant.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        var missing = await client.Http.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }));
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unknown = await TokenAsync(new ApiClient(_factory), "jp_unknown", "jps_nope");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-APIF-INVALID-CLIENT");
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-02")]
    public async Task ExpiredAccessToken_IsRejectedWithApifExpired()
    {
        var keys = _factory.Services.GetRequiredService<SigningKeyService>();
        var now = _factory.Clock.GetUtcNow().UtcDateTime;
        var handler = new JsonWebTokenHandler();
        var expired = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://identity.jobplatform.local",
            Audience = "jobplatform",
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", Guid.NewGuid().ToString()), new Claim("jti", Guid.NewGuid().ToString("N")),
                new Claim("actor_type", "ExternalJobSite"), new Claim("client_id", "jp_x"), new Claim("scope", "partner.api")
            }),
            NotBefore = now.AddHours(-3),
            IssuedAt = now.AddHours(-3),
            Expires = now.AddHours(-2),
            SigningCredentials = keys.GetSigningCredentials()
        });

        var response = await new ApiClient(_factory).Bearer(expired).Http.GetAsync("/api/v1/api-credentials/current");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-APIF-EXPIRED");
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-02")]
    public async Task IssuedToken_StopsWorkingOnceItsLifetimeEnds()
    {
        var (_, _, credential) = await IssuedAsync();
        var token = (await (await TokenAsync(new ApiClient(_factory), credential["clientId"]!.GetValue<string>(), credential["clientSecret"]!.GetValue<string>())).Json())["access_token"]!.GetValue<string>();
        var api = new ApiClient(_factory).Bearer(token);
        (await api.Http.GetAsync("/api/v1/api-credentials/current")).StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));

        await (await api.Http.GetAsync("/api/v1/api-credentials/current")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-APIF-EXPIRED");
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-03")]
    public async Task ClientCredentials_FiveFailedAttempts_BlockFurtherAttemptsWith429()
    {
        var (_, _, credential) = await IssuedAsync();
        var client = new ApiClient(_factory);
        for (var i = 0; i < 5; i++)
        {
            (await TokenAsync(client, credential["clientId"]!.GetValue<string>(), "jps_wrong")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var sixth = await TokenAsync(client, credential["clientId"]!.GetValue<string>(), credential["clientSecret"]!.GetValue<string>());

        await sixth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-APIF-RATE-LIMITED");
        sixth.Headers.RetryAfter.Should().NotBeNull();
        _factory.Clock.Advance(TimeSpan.FromMinutes(16));
        (await TokenAsync(client, credential["clientId"]!.GetValue<string>(), credential["clientSecret"]!.GetValue<string>())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-03")]
    public async Task ClientCredentials_CredentialLevelCap_HoldsEvenWhenTheAttackerRotatesSources()
    {
        var (_, _, credential) = await IssuedAsync();
        for (var i = 0; i < 5; i++)
        {
            await TokenAsync(new ApiClient(_factory), credential["clientId"]!.GetValue<string>(), "jps_wrong");
        }

        var sixth = await TokenAsync(new ApiClient(_factory), credential["clientId"]!.GetValue<string>(), credential["clientSecret"]!.GetValue<string>());

        await sixth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-APIF-RATE-LIMITED");
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-04")]
    public async Task Tokens_AndSecrets_NeverAppearInLogs()
    {
        var (_, _, credential) = await IssuedAsync();
        var secret = credential["clientSecret"]!.GetValue<string>();
        var clientId = credential["clientId"]!.GetValue<string>();
        var ok = await (await TokenAsync(new ApiClient(_factory), clientId, secret)).Json();
        var accessToken = ok["access_token"]!.GetValue<string>();
        await TokenAsync(new ApiClient(_factory), clientId, "jps_definitely_wrong_secret");
        await new ApiClient(_factory).Bearer(accessToken).Http.GetAsync("/api/v1/api-credentials/current");
        await new ApiClient(_factory).Bearer(accessToken + "x").Http.GetAsync("/api/v1/api-credentials/current");

        var lines = _factory.Logs.Lines;

        lines.Should().NotContain(l => l.Contains(secret) || l.Contains("jps_definitely_wrong_secret"));
        lines.Should().NotBeEmpty("the log capture must actually be wired, otherwise this test proves nothing");
        lines.Should().NotContain(l => l.Contains(accessToken));
        var access = await _factory.WithDbAsync(db => db.AccessLog.AsNoTracking().Where(l => l.Action == "oauth.token").ToListAsync());
        access.Should().NotBeEmpty();
        access.Should().OnlyContain(l => !(l.Reason ?? "").Contains(secret) && !(l.Reason ?? "").Contains(accessToken));
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-01")]
    public async Task ClientCredentials_FromANonWhitelistedIp_IsForbidden_AndDoesNotCountAsAFailedSecret()
    {
        var (_, _, credential) = await IssuedAsync(new { ipWhitelist = new[] { "203.0.113.0/24" } });
        var client = new ApiClient(_factory);

        for (var i = 0; i < 7; i++)
        {
            await (await TokenAsync(client, credential["clientId"]!.GetValue<string>(), credential["clientSecret"]!.GetValue<string>()))
                .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-TPJPRI-IP-NOT-ALLOWED");
        }
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-01")]
    public async Task RevokedCredential_InvalidatesTokensAlreadyIssued()
    {
        var (partner, _, credential) = await IssuedAsync();
        var token = (await (await TokenAsync(new ApiClient(_factory), credential["clientId"]!.GetValue<string>(), credential["clientSecret"]!.GetValue<string>())).Json())["access_token"]!.GetValue<string>();
        var api = new ApiClient(_factory).Bearer(token);
        (await api.Http.GetAsync("/api/v1/api-credentials/current")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await partner.PostAsync($"/api/v1/api-credentials/{credential["apiCredentialId"]!.GetValue<Guid>()}/revoke")).EnsureSuccessStatusCode();

        (await api.Http.GetAsync("/api/v1/api-credentials/current")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CredentialControls_AreAvailableToServicesForBc02()
    {
        var (_, partnerId, credential) = await IssuedAsync(new { ipWhitelist = new[] { "203.0.113.0/24" }, maxRequests = 10, periodSeconds = 5 });
        var service = await new ApiClient(_factory).ServiceAsync();

        var response = await service.Http.GetAsync($"/internal/v1/api-credentials/{credential["apiCredentialId"]!.GetValue<Guid>()}/controls");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["partnerAccountId"]!.GetValue<Guid>().Should().Be(partnerId);
        body["ipWhitelist"]!.AsArray().Single()!.GetValue<string>().Should().Be("203.0.113.0/24");
        body["maxRequests"]!.GetValue<int>().Should().Be(10);
        body["status"]!.GetValue<string>().Should().Be("Active");
        (await response.Content.ReadAsStringAsync()).Should().NotContainEquivalentOf("secret");
        await (await service.Http.GetAsync($"/internal/v1/api-credentials/{Guid.NewGuid()}/controls")).ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-API-CREDENTIAL-NOT-FOUND");
        (await new ApiClient(_factory).Http.GetAsync($"/internal/v1/api-credentials/{Guid.NewGuid()}/controls")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await partnerCannotCallInternal(partnerId)).Should().Be(HttpStatusCode.Forbidden);

        async Task<HttpStatusCode> partnerCannotCallInternal(Guid _)
        {
            var (partner, _, _) = await new ApiClient(_factory).ActivePartnerAsync();
            return (await partner.Http.GetAsync($"/internal/v1/api-credentials/{Guid.NewGuid()}/controls")).StatusCode;
        }
    }
}
