using System.Net;
using JobPlatform.AccountIdentity.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>Password policy (US-3.1.5-02). Own factory: the policy is a singleton and one test raises its version.</summary>
public class PasswordPolicyApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PasswordPolicyApiTests(ApiFactory factory) => _factory = factory;

    private async Task<(ApiClient User, string Mobile)> SeekerAsync(string password = "Str0ngPass")
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile, password: password);
        return (await client.UserAsync(mobile, password), mobile);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-01")]
    public async Task ChangePassword_MeetingThePolicy_IsAcceptedAndTheNewPasswordWorks()
    {
        var (user, mobile) = await SeekerAsync();

        var response = await user.PostAsync("/api/v1/auth/password", new { currentPassword = "Str0ngPass", newPassword = "Abcdef1x" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await user.LoginAsync(mobile, "Abcdef1x"))["status"]!.GetValue<string>().Should().Be("Authenticated");
        (await user.PostAsync("/api/v1/auth/login", new { username = mobile, password = "Str0ngPass", mechanism = "password" })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Sh0rt", "VAL.Password.MinLength")]
    [InlineData("alllowercase1", "VAL.Password.Uppercase")]
    [InlineData("ALLUPPERCASE1", "VAL.Password.Lowercase")]
    [InlineData("NoDigitsHere", "VAL.Password.Digit")]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-02")]
    public async Task ChangePassword_NotMeetingThePolicy_Returns400InvalidFieldNamingTheViolatedRule(string password, string violation)
    {
        var (user, _) = await SeekerAsync();

        var response = await user.PostAsync("/api/v1/auth/password", new { currentPassword = "Str0ngPass", newPassword = password });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-AAFR-INVALID-FIELD");
        var body = await response.Json();
        body["errors"]!["password"]!.AsArray().Select(v => v!.GetValue<string>()).Should().Contain(violation);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(password, "the rejected password is never echoed");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-02")]
    public async Task ChangePassword_WrongCurrentPasswordOrSameAsCurrent_IsRejected()
    {
        var (user, _) = await SeekerAsync();

        var wrong = await user.PostAsync("/api/v1/auth/password", new { currentPassword = "Wr0ngPassword", newPassword = "Abcdef1x" });
        await wrong.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-INVALID-CREDENTIALS");

        var same = await user.PostAsync("/api/v1/auth/password", new { currentPassword = "Str0ngPass", newPassword = "Str0ngPass" });
        await same.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await same.Json())["errors"]!["newPassword"]!.AsArray().Select(v => v!.GetValue<string>()).Should().Contain("VAL.NewPassword.SameAsCurrent");

        var anonymous = await new ApiClient(_factory).PostAsync("/api/v1/auth/password", new { currentPassword = "a", newPassword = "b" });
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-04")]
    public async Task Password_IsStoredOnlyAsAnAdaptiveHash_NeverPlaintext_AndNeverInTheLogs()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        const string password = "Un1queSecretPw9";
        var id = await client.ActiveJobSeekerAsync(mobile, password: password);
        var user = await client.UserAsync(mobile, password);
        await user.PostAsync("/api/v1/auth/password", new { currentPassword = password, newPassword = "An0therSecretPw" });

        var stored = await _factory.WithDbAsync(async db =>
            (await db.Accounts.AsNoTracking().Where(a => a.Id == new AccountId(id)).Select(a => a.PasswordHash).SingleAsync()).Value);
        var everyTextColumn = await _factory.WithDbAsync(db => db.Database.SqlQueryRaw<string>("SELECT PasswordHash || DisplayName || Mobile AS Value FROM Accounts").ToListAsync());

        stored.Should().StartWith("$pbkdf2-sha512$i=").And.NotContain("An0therSecretPw").And.NotContain(password);
        everyTextColumn.Should().OnlyContain(row => !row.Contains(password) && !row.Contains("An0therSecretPw"));
        _factory.Logs.Lines.Should().NotContain(l => l.Contains(password) || l.Contains("An0therSecretPw"));
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-03")]
    public async Task PolicyConfiguration_IsAdministratorOnly_AndValidated()
    {
        var (user, _) = await SeekerAsync();
        var admin = await new ApiClient(_factory).AdminAsync();

        (await user.PutAsync("/api/v1/admin/password-policy", new { minLength = 10, requireUpper = true, requireLower = true, requireDigit = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await new ApiClient(_factory).PutAsync("/api/v1/admin/password-policy", new { minLength = 10 })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var tooWeak = await admin.PutAsync("/api/v1/admin/password-policy", new { minLength = 4, requireUpper = false, requireLower = false, requireDigit = false });
        await tooWeak.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await tooWeak.Json())["errors"]!.AsObject().Select(e => e.Key).Should().Contain(new[] { "minLength", "characterClasses" });
    }
}

/// <summary>Changing the singleton policy affects every later registration, so each such scenario gets its own host and database.</summary>
public class PasswordPolicyChangeApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PasswordPolicyChangeApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-03")]
    public async Task PolicyChange_AppliesOnlyAtTheNextPasswordChange_ExistingUsersAreNotForced()
    {
        var existingClient = new ApiClient(_factory);
        var existingMobile = ApiClient.NewMobile();
        await existingClient.ActiveJobSeekerAsync(existingMobile);
        var existing = await existingClient.UserAsync(existingMobile, "Str0ngPass");
        var admin = await new ApiClient(_factory).AdminAsync();

        var update = await admin.PutAsync("/api/v1/admin/password-policy", new { minLength = 12, requireUpper = true, requireLower = true, requireDigit = true });
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var policy = await (await admin.Http.GetAsync("/api/v1/admin/password-policy")).Json();
        policy["minLength"]!.GetValue<int>().Should().Be(12);
        policy["policyVersion"]!.GetValue<int>().Should().Be(2);
        (await existing.LoginAsync(existingMobile, "Str0ngPass"))["status"]!.GetValue<string>().Should().Be("Authenticated", "nobody is forced to change");

        var tooShort = await existing.PostAsync("/api/v1/auth/password", new { currentPassword = "Str0ngPass", newPassword = "Abcdef1xyz" });
        await tooShort.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-AAFR-INVALID-FIELD");
        (await existing.PostAsync("/api/v1/auth/password", new { currentPassword = "Str0ngPass", newPassword = "Abcdefgh1234" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var newRegistration = await new ApiClient(_factory).PostAsync("/api/v1/accounts/job-seekers",
            new { fullName = "New", mobile = ApiClient.NewMobile(), password = "Abcdef1x" });
        await newRegistration.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-AAFR-INVALID-FIELD");
    }

}

public class PasswordPolicyCacheApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PasswordPolicyCacheApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-03")]
    public async Task PolicyRead_IsServedFromTheCache_AndEvictedWhenTheAdministratorChangesIt()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var before = await (await admin.Http.GetAsync("/api/v1/admin/password-policy")).Json();
        var beforeVersion = before["policyVersion"]!.GetValue<int>();

        (await admin.PutAsync("/api/v1/admin/password-policy", new { minLength = 14, requireUpper = true, requireLower = true, requireDigit = true })).EnsureSuccessStatusCode();
        var after = await (await admin.Http.GetAsync("/api/v1/admin/password-policy")).Json();

        after["policyVersion"]!.GetValue<int>().Should().Be(beforeVersion + 1, "the cached policy must be evicted by ConfigurePasswordPolicy");
        after["minLength"]!.GetValue<int>().Should().Be(14);
    }
}

/// <summary>Session timeout, sliding window, logout and credential reset (US-3.1.5-04, US-3.1.4-03 AC-02).</summary>
public class SessionApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SessionApiTests(ApiFactory factory) => _factory = factory;

    private static Task<HttpResponseMessage> ProbeAsync(ApiClient admin) => admin.Http.GetAsync("/api/v1/admin/session-timeout");

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-01")]
    public async Task IdleSession_ExpiresAfterThirtyMinutes_AndTheUserMustAuthenticateAgain()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        (await ProbeAsync(admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        _factory.Clock.Advance(TimeSpan.FromMinutes(29));
        (await ProbeAsync(admin)).StatusCode.Should().Be(HttpStatusCode.OK, "29 minutes idle is still inside the window");

        _factory.Clock.Advance(TimeSpan.FromMinutes(30));
        var expired = await ProbeAsync(admin);

        await expired.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
        (await admin.AdminAsync()).Should().NotBeNull("signing in again works");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-02")]
    public async Task Activity_ResetsTheIdleTimer()
    {
        var admin = await new ApiClient(_factory).AdminAsync();

        for (var i = 0; i < 4; i++)
        {
            _factory.Clock.Advance(TimeSpan.FromMinutes(20));
            (await ProbeAsync(admin)).StatusCode.Should().Be(HttpStatusCode.OK, $"activity at +{(i + 1) * 20} minutes keeps the session alive");
        }

        _factory.Clock.Advance(TimeSpan.FromMinutes(31));
        (await ProbeAsync(admin)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-02")]
    public async Task Refresh_CountsAsActivity_AndAnExpiredSessionCannotBeRefreshed()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        await client.ActiveJobSeekerAsync(mobile);
        var (_, refresh) = await client.TokensAsync(mobile, "Str0ngPass");

        _factory.Clock.Advance(TimeSpan.FromMinutes(25));
        var refreshed = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = refresh });
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var newRefresh = (await refreshed.Json())["tokens"]!["refreshToken"]!.GetValue<string>();

        _factory.Clock.Advance(TimeSpan.FromMinutes(31));
        var late = await client.PostAsync("/api/v1/auth/refresh", new { refreshToken = newRefresh });
        await late.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-03")]
    public async Task ChangingTheTimeout_OnlyAffectsSessionsCreatedAfterwards()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var existingSession = await new ApiClient(_factory).AdminAsync();
        (await admin.PutAsync("/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 5 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        try
        {
            var newSession = await new ApiClient(_factory).AdminAsync();

            _factory.Clock.Advance(TimeSpan.FromMinutes(10));

            (await ProbeAsync(existingSession)).StatusCode.Should().Be(HttpStatusCode.OK, "it captured the 30 minute timeout when it was created");
            (await ProbeAsync(newSession)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "it captured the new 5 minute timeout");

            var view = await (await new ApiClient(_factory).AdminAsync()).Http.GetAsync("/api/v1/admin/session-timeout");
            (await view.Json())["idleTimeoutMinutes"]!.GetValue<int>().Should().Be(5);
        }
        finally
        {
            var restore = await new ApiClient(_factory).AdminAsync();
            await restore.PutAsync("/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 30 });
        }
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-03")]
    public async Task SessionTimeoutConfiguration_IsValidatedAndAdministratorOnly()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var mobile = ApiClient.NewMobile();
        await admin.ActiveJobSeekerAsync(mobile);
        var user = await admin.UserAsync(mobile, "Str0ngPass");

        (await admin.PutAsync("/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 2 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.PutAsync("/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 481 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await user.PutAsync("/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 30 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PutAsync("/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 30 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-04")]
    public async Task Logout_MakesTheAdministratorTokenUnusableAtOnce()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        (await ProbeAsync(admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.PostAsync("/api/v1/auth/logout")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await (await ProbeAsync(admin)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-02")]
    public async Task ResetCredentials_EndsSessions_AndForcesAPasswordChangeAtNextLogin()
    {
        var client = new ApiClient(_factory);
        var (user, id, email) = await client.ActivePartnerAsync();
        var admin = await client.AdminAsync();

        (await admin.PostAsync($"/api/v1/admin/accounts/{id}/reset-credentials")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await (await user.PostAsync("/api/v1/auth/logout")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-SESSION-EXPIRED");
        var login = await client.LoginAsync(email, "Str0ngPass");
        login["status"]!.GetValue<string>().Should().Be("Authenticated");
        login["mustChangePassword"]!.GetValue<bool>().Should().BeTrue();

        var restricted = new ApiClient(_factory).Bearer(login["tokens"]!["accessToken"]!.GetValue<string>());
        var blocked = await restricted.Http.GetAsync("/api/v1/api-credentials/current");
        await blocked.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AAFR-PASSWORD-CHANGE-REQUIRED");
        (await restricted.PostAsync("/api/v1/auth/password", new { currentPassword = "Str0ngPass", newPassword = "Brand9NewPass" })).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        (await client.LoginAsync(email, "Brand9NewPass"))["mustChangePassword"]!.GetValue<bool>().Should().BeFalse();
    }
}
