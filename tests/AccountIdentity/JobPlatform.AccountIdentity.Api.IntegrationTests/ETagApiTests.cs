using System.Net;
using System.Net.Http.Json;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

/// <summary>ETag on reads and If-Match on updates (foundation section 11): a stale precondition is answered with 412 and changes nothing.</summary>
public class ETagApiTests : IClassFixture<ApiFactory>
{
    private const string Stale = "\"AAAAAAAAAAA=\"";

    private readonly ApiFactory _factory;

    public ETagApiTests(ApiFactory factory) => _factory = factory;

    private static Task<HttpResponseMessage> SendAsync(ApiClient client, HttpMethod method, string url, object? body = null, string? ifMatch = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return client.Http.SendAsync(request);
    }

    private static string ETagOf(HttpResponseMessage response) => response.Headers.ETag?.Tag ?? throw new InvalidOperationException("No ETag header");

    [Fact]
    public async Task PasswordPolicy_ReadCarriesAnETag_AndAnUpdateWithAStaleOrOutdatedTagIsRefused()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var read = await admin.Http.GetAsync("/api/v1/admin/password-policy");
        var tag = ETagOf(read);
        var body = new { minLength = 10, requireUpper = true, requireLower = true, requireDigit = true };

        var stale = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/password-policy", body, Stale);
        var ok = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/password-policy", body, tag);
        var outdated = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/password-policy", new { minLength = 11, requireUpper = true, requireLower = true, requireDigit = true }, tag);
        var reread = await admin.Http.GetAsync("/api/v1/admin/password-policy");

        await stale.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "E-PRECONDITION-FAILED");
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await outdated.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "E-PRECONDITION-FAILED");
        ETagOf(reread).Should().NotBe(tag, "the update changed the resource version");
        (await reread.Json())["minLength"]!.GetValue<int>().Should().Be(10, "only the first, valid update was applied");
    }

    [Fact]
    public async Task PasswordPolicy_WithoutIfMatch_StillUpdates_AndStarMatchesAnyVersion()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var body = new { minLength = 9, requireUpper = true, requireLower = true, requireDigit = true };

        (await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/password-policy", body)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/password-policy", body, "*")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SessionTimeout_ReadCarriesAnETag_AndAStaleUpdateIsRefused()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var tag = ETagOf(await admin.Http.GetAsync("/api/v1/admin/session-timeout"));

        var stale = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 45 }, Stale);
        var ok = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 45 }, tag);

        await stale.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "E-PRECONDITION-FAILED");
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        ETagOf(await admin.Http.GetAsync("/api/v1/admin/session-timeout")).Should().NotBe(tag);
        // restore the default so the other API tests keep their expectations
        (await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 30 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AccountStanding_ReadCarriesAnETag_AndAStaleAdministratorActionIsRefusedWithoutEffect()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var seeker = new ApiClient(_factory);
        var id = await seeker.ActiveJobSeekerAsync();
        var read = await admin.Http.GetAsync($"/api/v1/admin/accounts/{id}");
        var tag = ETagOf(read);

        var stale = await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/accounts/{id}/ban", new { reason = "abuse" }, Stale);
        var standingAfterStale = (await (await admin.Http.GetAsync($"/api/v1/admin/accounts/{id}")).Json())["standing"]!.GetValue<string>();
        var ok = await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/accounts/{id}/ban", new { reason = "abuse" }, tag);

        await stale.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "E-PRECONDITION-FAILED");
        standingAfterStale.Should().Be("Active");
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await admin.Http.GetAsync($"/api/v1/admin/accounts/{id}")).Json())["standing"]!.GetValue<string>().Should().Be("Banned");
    }

    [Fact]
    public async Task Roles_ExposeAnETagPerRole_AndAStalePermissionChangeIsRefused()
    {
        var admin = await new ApiClient(_factory).AdminAsync();
        var roles = (await (await admin.Http.GetAsync("/api/v1/admin/roles")).Json()).AsArray();
        var role = roles.First(r => r!["name"]!.GetValue<string>() == "Guest")!;
        var roleId = role["roleId"]!.GetValue<Guid>();
        var tag = role["eTag"]!.GetValue<string>();
        tag.Should().StartWith("\"");

        var stale = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/roles/{roleId}/permissions/accounts.read", ifMatch: Stale);
        var ok = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/roles/{roleId}/permissions/accounts.read", ifMatch: tag);
        var cleanup = await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/roles/{roleId}/permissions/accounts.read");

        await stale.ShouldBeProblemAsync(HttpStatusCode.PreconditionFailed, "E-PRECONDITION-FAILED");
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cleanup.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PreconditionFailed_IsLocalisedInArabic()
    {
        var admin = (await new ApiClient(_factory).AdminAsync()).WithLanguage("ar");

        var response = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/session-timeout", new { idleTimeoutMinutes = 40 }, Stale);

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await response.Json())["detail"]!.GetValue<string>().Should().Contain("ETag");
        (await response.Json())["detail"]!.GetValue<string>().Should().Contain("تم تعديل المورد");
    }
}
