using System.Net;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

public class AuthenticationApiTests : IClassFixture<ApiFactory>
{
    private const string Password = "Str0ngPass";
    private readonly ApiFactory _factory;

    public AuthenticationApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-01")]
    public async Task Login_WithValidCredentials_ReturnsTokensAndCreatesASession()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);

        var result = await client.LoginAsync(mobile, Password);

        result["status"]!.GetValue<string>().Should().Be("Authenticated");
        var tokens = result["tokens"]!;
        tokens["tokenType"]!.GetValue<string>().Should().Be("Bearer");
        tokens["accessToken"]!.GetValue<string>().Split('.').Should().HaveCount(3, "a JWT");
        tokens["refreshToken"]!.GetValue<string>().Should().Contain(".");
        tokens["expiresInSeconds"]!.GetValue<int>().Should().BeGreaterThan(0);
        // The session exists: the token is accepted by an authenticated endpoint.
        var authed = await new ApiClient(_factory).Bearer(tokens["accessToken"]!.GetValue<string>()).PostAsync("/api/v1/auth/logout");
        authed.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-01")]
    public async Task Login_ByEmail_AlsoWorks()
    {
        var client = new ApiClient(_factory);
        var email = ApiClient.NewEmail();
        await client.ActiveJobSeekerAsync(email: email);

        (await client.LoginAsync(email.ToUpperInvariant(), Password))["status"]!.GetValue<string>().Should().Be("Authenticated");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-04")]
    public async Task Login_UnknownUserAndWrongPassword_ReturnTheSameGenericError()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);

        var wrongPassword = await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Wr0ngPassword", mechanism = "password" });
        var unknownUser = await client.PostAsync("/api/v1/auth/login", new { username = ApiClient.NewMobile(), password = "Wr0ngPassword", mechanism = "password" });

        await wrongPassword.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-INVALID-CREDENTIALS");
        await unknownUser.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-INVALID-CREDENTIALS");
        var a = await wrongPassword.Json();
        var b = await unknownUser.Json();
        foreach (var field in new[] { "type", "title", "status", "detail", "code" })
        {
            a[field]!.ToJsonString().Should().Be(b[field]!.ToJsonString(), $"'{field}' must not reveal which credential was wrong");
        }

        (await wrongPassword.Content.ReadAsStringAsync()).Should().NotContain("password", "the message must not name the field");
        wrongPassword.Headers.WwwAuthenticate.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-03")]
    public async Task Login_FiveFailedAttempts_LockTheAccountForFifteenMinutes()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);

        for (var i = 1; i <= 4; i++)
        {
            (await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Wr0ngPassword", mechanism = "password" }))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"attempt {i}");
        }

        var fifth = await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Wr0ngPassword", mechanism = "password" });
        await fifth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-AAFR-RATE-LIMITED");
        fifth.Headers.RetryAfter.Should().NotBeNull();

        // Locked: even the correct password is refused.
        var correct = await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = Password, mechanism = "password" });
        await correct.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-AAFR-RATE-LIMITED");
        (await correct.Json())["ruleCode"]!.GetValue<string>().Should().Be("AI.Account.LOCKED");

        _factory.Clock.Advance(TimeSpan.FromMinutes(15));
        (await client.LoginAsync(mobile, Password))["status"]!.GetValue<string>().Should().Be("Authenticated");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-03")]
    public async Task Login_Failures_ArePersistedEvenThoughTheRequestFails()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var id = await client.ActiveJobSeekerAsync(mobile);

        await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Wr0ngPassword", mechanism = "password" });
        await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Wr0ngPassword", mechanism = "password" });

        var failed = await _factory.WithDbAsync(db => db.Accounts.AsNoTracking().Where(a => a.Id == new JobPlatform.AccountIdentity.Domain.Common.AccountId(id)).Select(a => a.FailedAttempts).SingleAsync());
        failed.Should().Be(2);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_SupportsPasswordEmailVerificationAndMfaMechanisms()
    {
        var client = new ApiClient(_factory);

        // 1. username / password
        var mobile = ApiClient.NewMobile();
        var email = ApiClient.NewEmail();
        var id = await client.ActiveJobSeekerAsync(mobile, email);
        (await client.LoginAsync(mobile, Password))["status"]!.GetValue<string>().Should().Be("Authenticated");

        // 2. e-mail verification: verify ownership of the address, then sign in with a one-time code sent to it
        var token = _factory.Messages.LastFor("email-verification", email)!.Code!;
        (await client.PostAsync("/api/v1/auth/email-verification", new { accountId = id, token })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var requested = await client.PostAsync("/api/v1/auth/login", new { username = email, mechanism = "email-verification" });
        (await requested.Json())["status"]!.GetValue<string>().Should().Be("EmailCodeSent");
        var code = _factory.Messages.LastFor("login-code", email)!.Code!;
        var completed = await client.PostAsync("/api/v1/auth/login", new { username = email, mechanism = "email-verification", emailCode = code });
        (await completed.Json())["status"]!.GetValue<string>().Should().Be("Authenticated");
        var replay = await client.PostAsync("/api/v1/auth/login", new { username = email, mechanism = "email-verification", emailCode = code });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a login code is single use");

        // 3. multi-factor: administrator signs in with password + TOTP in one call
        var admin = await client.AdminAsync();
        admin.Should().NotBeNull();
        var withMfa = await client.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword, "mfa", _factory.Totp.For(_factory.AdminMfaSecret!));
        withMfa["status"]!.GetValue<string>().Should().Be("Authenticated");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_EmailVerificationCodeRequest_DoesNotRevealWhetherTheEmailIsRegistered()
    {
        var client = new ApiClient(_factory);

        var unknown = await client.PostAsync("/api/v1/auth/login", new { username = ApiClient.NewEmail(), mechanism = "email-verification" });

        unknown.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unknown.Json())["status"]!.GetValue<string>().Should().Be("EmailCodeSent");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_InvalidMechanismOrMissingFields_Returns400()
    {
        var client = new ApiClient(_factory);

        var badMechanism = await client.PostAsync("/api/v1/auth/login", new { username = "x@y.com", password = "p", mechanism = "biometrics" });
        await badMechanism.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await badMechanism.Json())["errors"]!["mechanism"].Should().NotBeNull();

        var missing = await client.PostAsync("/api/v1/auth/login", new { username = "", password = "", mechanism = "password" });
        var errors = (await missing.Json())["errors"]!.AsObject();
        errors["username"].Should().NotBeNull();
        errors["password"].Should().NotBeNull();

        var mfaWithoutCode = await client.PostAsync("/api/v1/auth/login", new { username = "a@b.com", password = "p", mechanism = "mfa" });
        (await mfaWithoutCode.Json())["errors"]!["mfaCode"].Should().NotBeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task Login_Administrator_MustCompleteMfa_AndGetsNoTokensFromThePasswordStepAlone()
    {
        var client = new ApiClient(_factory);
        await client.AdminAsync();

        var passwordOnly = await client.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

        passwordOnly["status"]!.GetValue<string>().Should().Be("MfaRequired");
        passwordOnly["tokens"].Should().BeNull("THR-029: administrator access needs the second factor");
        passwordOnly["mfaToken"].Should().NotBeNull();

        var wrongCode = await client.PostAsync("/api/v1/auth/mfa/verify", new { mfaToken = passwordOnly["mfaToken"]!.GetValue<string>(), code = "000000" });
        await wrongCode.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-INVALID-CREDENTIALS");

        var bogus = await client.PostAsync("/api/v1/auth/mfa/verify", new { mfaToken = "not-a-real-token", code = "123456" });
        await bogus.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-INVALID-CREDENTIALS");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-03")]
    public async Task Mfa_FiveWrongCodes_LockTheAdministrator()
    {
        var client = new ApiClient(_factory);
        await client.AdminAsync();
        var login = await client.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        var mfaToken = login["mfaToken"]!.GetValue<string>();
        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await client.PostAsync("/api/v1/auth/mfa/verify", new { mfaToken, code = "000000" });
        }

        await last.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-AAFR-RATE-LIMITED");
        _factory.Clock.Advance(TimeSpan.FromMinutes(16));
        (await client.AdminAsync()).Should().NotBeNull("the lock expires after 15 minutes");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-04")]
    public async Task Login_PendingAccount_WithCorrectPassword_IsRefusedWith403()
    {
        var client = new ApiClient(_factory);
        var (_, mobile, _) = await client.RegisterJobSeekerAsync();

        var response = await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = Password, mechanism = "password" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AAFR-ACCOUNT-PENDING");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public async Task VerifyEmail_WrongOrUnknown_IsRejected()
    {
        var client = new ApiClient(_factory);
        var email = ApiClient.NewEmail();
        var (id, _, _) = await client.RegisterJobSeekerAsync(email: email);

        var wrong = await client.PostAsync("/api/v1/auth/email-verification", new { accountId = id, token = "nope" });
        await wrong.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-AAFR-INVALID-FIELD");

        var unknown = await client.PostAsync("/api/v1/auth/email-verification", new { accountId = Guid.NewGuid(), token = "nope" });
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-ACCOUNT-NOT-FOUND");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-04")]
    public async Task Logout_InvalidatesTheAccessTokenAndTheRefreshTokenImmediately()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);
        var (access, refresh) = await client.TokensAsync(mobile, Password);
        var user = new ApiClient(_factory).Bearer(access);

        (await user.PostAsync("/api/v1/auth/logout")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var again = await user.PostAsync("/api/v1/auth/logout");
        await again.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
        var refreshed = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        await refreshed.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-02")]
    public async Task Refresh_RotatesTheRefreshToken_AndReplayingTheOldOneKillsTheSession()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);
        var (_, refresh) = await client.TokensAsync(mobile, Password);

        var first = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var rotated = (await first.Json())["tokens"]!["refreshToken"]!.GetValue<string>();
        rotated.Should().NotBe(refresh);

        var replay = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        await replay.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
        var afterTheft = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = rotated });
        afterTheft.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a replayed refresh token means theft: the session is dead");
    }

    [Fact]
    public async Task Refresh_MalformedToken_Returns401()
    {
        var client = new ApiClient(_factory);

        (await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = "garbage" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = Guid.NewGuid().ToString("N") + ".secret" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-04")]
    public async Task ProtectedEndpoint_WithoutOrWithGarbageToken_Returns401Problem()
    {
        var anonymous = await new ApiClient(_factory).PostAsync("/api/v1/auth/logout");
        await anonymous.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-UNAUTHORIZED");

        var garbage = await new ApiClient(_factory).Bearer("not.a.jwt").PostAsync("/api/v1/auth/logout");
        await garbage.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-UNAUTHORIZED");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-04")]
    public async Task AccessLog_RecordsLoginSuccessAndFailure_WithoutCredentials()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var id = await client.ActiveJobSeekerAsync(mobile);
        await client.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Wr0ngPassword", mechanism = "password" });
        await client.LoginAsync(mobile, Password);

        var rows = await _factory.WithDbAsync(db => db.AccessLog.AsNoTracking().Where(l => l.AccountId == id && l.Action == "auth.login").ToListAsync());

        rows.Should().Contain(r => r.Decision == "Deny").And.Contain(r => r.Decision == "Allow");
        rows.Should().OnlyContain(r => !(r.Reason ?? "").Contains("Wr0ng") && !(r.Reason ?? "").Contains(Password));
    }

    [Fact]
    public async Task Passwords_AreStoredAsPbkdf2Hashes_NeverAsPlaintext()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var id = await client.ActiveJobSeekerAsync(mobile, password: "Pl4intextCheck");

        var hash = await _factory.WithDbAsync(async db =>
            (await db.Accounts.AsNoTracking().Where(a => a.Id == new JobPlatform.AccountIdentity.Domain.Common.AccountId(id)).Select(a => a.PasswordHash).SingleAsync()).Value);

        hash.Should().StartWith("$pbkdf2-sha512$").And.NotContain("Pl4intextCheck");
    }
}
