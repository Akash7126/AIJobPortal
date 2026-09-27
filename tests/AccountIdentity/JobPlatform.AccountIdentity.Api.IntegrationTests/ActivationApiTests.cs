using System.Net;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

public class ActivationApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ActivationApiTests(ApiFactory factory) => _factory = factory;

    private async Task<(ApiClient Client, Guid Id, string Mobile)> PendingSeekerAsync()
    {
        var client = new ApiClient(_factory);
        var (id, mobile, _) = await client.RegisterJobSeekerAsync();
        return (client, id, mobile);
    }

    private async Task<(ApiClient Client, Guid Id, string Mobile)> PendingEmployerAsync()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var response = await client.PostAsync("/api/v1/accounts/employers", new
        {
            companyName = "Acme", email = ApiClient.NewEmail(), mobile, companyId = "C-" + Guid.NewGuid().ToString("N"),
            registrationNumber = "R-1", password = "Str0ngPass", level = 1
        });
        response.EnsureSuccessStatusCode();
        return (client, (await response.Json())["accountId"]!.GetValue<Guid>(), mobile);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-01")]
    public async Task Activate_WithCorrectCode_Returns204AndTheAccountBecomesActive()
    {
        var (client, id, mobile) = await PendingSeekerAsync();

        var response = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = client.LastCode(mobile) });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var summary = await (await (await client.ServiceAsync()).Http.GetAsync($"/internal/v1/accounts/{id}")).Json();
        summary["standing"]!.GetValue<string>().Should().Be("Active");
        (await client.LoginAsync(mobile, "Str0ngPass"))["status"]!.GetValue<string>().Should().Be("Authenticated");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-02")]
    [Trait("AC", "AC-01")]
    public async Task Activate_EmployerWithCorrectOtp_BecomesActive()
    {
        var (client, id, mobile) = await PendingEmployerAsync();

        var response = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = client.LastCode(mobile) });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-02")]
    public async Task Activate_WithWrongCode_Returns422JobSeekerExpired()
    {
        var (client, id, _) = await PendingSeekerAsync();

        var response = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = "000000" });

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-JSRPM-EXPIRED");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-02")]
    public async Task Activate_CodeOlderThanTenMinutes_Returns422Expired_AndAFreshCodeWorks()
    {
        var (client, id, mobile) = await PendingSeekerAsync();
        var oldCode = client.LastCode(mobile);
        _factory.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        var expired = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = oldCode });
        await expired.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-JSRPM-EXPIRED");

        (await client.PostAsync($"/api/v1/accounts/{id}/activation-code")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var fresh = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = client.LastCode(mobile) });
        fresh.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-02")]
    [Trait("AC", "AC-02")]
    public async Task Activate_EmployerWithWrongOtp_Returns422EmployerExpired()
    {
        var (client, id, _) = await PendingEmployerAsync();

        var response = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = "000000" });

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "E-ERPM-EXPIRED");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-03")]
    public async Task Activate_SixthAttemptFromTheSameSource_IsBlockedWith429_EvenWithTheCorrectCode()
    {
        var (client, id, mobile) = await PendingSeekerAsync();
        var correct = client.LastCode(mobile);
        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = "000000" })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }

        var sixth = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = correct });

        await sixth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-JSRPM-RATE-LIMITED");
        var after = await (await (await client.ServiceAsync()).Http.GetAsync($"/internal/v1/accounts/{id}")).Json();
        after["standing"]!.GetValue<string>().Should().Be("Pending");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-03")]
    public async Task Activate_AttemptCapIsEnforcedByTheAccountEvenWhenSourcesChange()
    {
        var (client, id, mobile) = await PendingSeekerAsync();
        var correct = client.LastCode(mobile);
        for (var i = 0; i < 5; i++)
        {
            await new ApiClient(_factory).PostAsync($"/api/v1/accounts/{id}/activate", new { code = "000000" });
        }

        var sixth = await new ApiClient(_factory).PostAsync($"/api/v1/accounts/{id}/activate", new { code = correct });

        await sixth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-JSRPM-RATE-LIMITED");
        (await sixth.Json())["ruleCode"]!.GetValue<string>().Should().Be("AI.Account.TOO_MANY_ATTEMPTS");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-02")]
    [Trait("AC", "AC-03")]
    public async Task Activate_EmployerSixthOtpAttempt_IsBlockedWithEmployerRateLimited()
    {
        var (client, id, _) = await PendingEmployerAsync();
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = "000000" });
        }

        var sixth = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = "111111" });

        await sixth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-ERPM-RATE-LIMITED");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-04")]
    public async Task Activate_PublishesAccountApprovedWithJobSeekerActorType()
    {
        var (client, id, mobile) = await PendingSeekerAsync();

        (await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = client.LastCode(mobile) })).EnsureSuccessStatusCode();

        var approved = await _factory.PublishedEventAsync("AccountApproved", id);
        approved["accountId"]!.GetValue<Guid>().Should().Be(id);
        approved["actorType"]!.GetValue<string>().Should().Be("JobSeeker", "BC-04 acts on job seekers only");
        approved["aggregateVersion"]!.GetValue<long>().Should().BeGreaterThan(1);
        var row = (await _factory.OutboxAsync("AccountApproved", id.ToString())).Single();
        row.RoutingKey.Should().Be("account.approved.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-02")]
    [Trait("AC", "AC-04")]
    public async Task Activate_Employer_PublishesAccountApprovedWithEmployerActorType()
    {
        var (client, id, mobile) = await PendingEmployerAsync();

        (await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = client.LastCode(mobile) })).EnsureSuccessStatusCode();

        (await _factory.PublishedEventAsync("AccountApproved", id))["actorType"]!.GetValue<string>().Should().Be("Employer", "BC-05 acts on employers only");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-02")]
    public async Task Activate_MalformedCodeOrUnknownAccount_ReturnsValidationOrNotFound()
    {
        var (client, id, _) = await PendingSeekerAsync();

        var badFormat = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = "12ab" });
        await badFormat.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await badFormat.Json())["errors"]!["code"]!.AsArray().Select(e => e!.GetValue<string>()).Should().Contain("VAL.Code.Format");

        var unknown = await client.PostAsync($"/api/v1/accounts/{Guid.NewGuid()}/activate", new { code = "123456" });
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-ACCOUNT-NOT-FOUND");
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-01")]
    public async Task ResendActivationCode_IssuesANewCodeAndInvalidatesTheOldOne()
    {
        var (client, id, mobile) = await PendingSeekerAsync();
        var first = client.LastCode(mobile);
        (await client.PostAsync($"/api/v1/accounts/{id}/activation-code")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var second = client.LastCode(mobile);

        second.Should().NotBe(first);
        (await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = first })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code = second })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-03")]
    public async Task ResendActivationCode_IsRateLimitedPerSource()
    {
        var (client, id, _) = await PendingSeekerAsync();
        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsync($"/api/v1/accounts/{id}/activation-code")).StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        var sixth = await client.PostAsync($"/api/v1/accounts/{id}/activation-code");

        await sixth.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "E-JSRPM-RATE-LIMITED");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-03")]
    public async Task Activate_BannedAccount_CannotBeActivated()
    {
        var (client, id, mobile) = await PendingSeekerAsync();
        var code = client.LastCode(mobile);
        var admin = await client.AdminAsync();
        (await admin.PostAsync($"/api/v1/admin/accounts/{id}/ban", new { reason = "fraud" })).EnsureSuccessStatusCode();

        var response = await client.PostAsync($"/api/v1/accounts/{id}/activate", new { code });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-AUM-STATE-BANNED");
    }
}
