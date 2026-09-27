using System.Net;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.TestSupport;

namespace JobPlatform.PlatformAdministration.Api.IntegrationTests;

public class SecurityAndUsersTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SecurityAndUsersTests(ApiFactory factory) => _factory = factory;

    private const string Id = "00000000-0000-0000-0000-000000000001";

    public static IEnumerable<object[]> AdminRoutes() => new[]
    {
        new object[] { "GET", "/api/v1/admin/users" },
        new object[] { "POST", "/api/v1/admin/entity-records" },
        new object[] { "GET", $"/api/v1/admin/entity-records/{Id}" },
        new object[] { "GET", "/api/v1/admin/settings" },
        new object[] { "PUT", "/api/v1/admin/settings/upload.maxSizeMb" },
        new object[] { "GET", "/api/v1/admin/reference-files/skills" },
        new object[] { "PUT", "/api/v1/admin/reference-files/skills/entries" },
        new object[] { "GET", "/api/v1/admin/taxonomies/skills" },
        new object[] { "PUT", "/api/v1/admin/taxonomies/skills/nodes" },
        new object[] { "GET", "/api/v1/admin/job-offerings" },
        new object[] { "POST", $"/api/v1/admin/job-offerings/{Id}/suspend" },
        new object[] { "POST", $"/api/v1/admin/job-offerings/{Id}/remove" }
    };

    private static HttpRequestMessage Request(string method, string url) =>
        new(new HttpMethod(method), url) { Content = method == "GET" ? null : JsonContent.Create(new { }) };

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    [Trait("Story", "US-3.1.4-01")]
    [Trait("AC", "AC-04")]
    public async Task AdminRoute_Anonymous_Is401(string method, string url)
    {
        var response = await _factory.ClientFor(null).SendAsync(Request(method, url));

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "E-AAFR-UNAUTHORIZED");
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task AdminRoute_NonAdministrators_AreForbiddenWithTheStoryCode(string method, string url)
    {
        foreach (var token in new[] { TestTokens.JobSeeker(), TestTokens.Employer(), TestTokens.Partner(), TestTokens.Issue(ActorType.Administrator, mfa: false) })
        {
            var response = await _factory.ClientFor(token).SendAsync(Request(method, url));

            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-AUM-FORBIDDEN");
        }
    }

    [Theory]
    [InlineData("/internal/v1/taxonomies/skills")]
    [InlineData("/internal/v1/reference-files/skills")]
    [InlineData("/internal/v1/settings/upload.maxSizeMb")]
    public async Task InternalRoute_RequiresTheServiceToken(string url)
    {
        (await _factory.ClientFor(null).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _factory.ClientFor(TestTokens.Admin()).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _factory.ClientFor(TestTokens.Service()).GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Forbidden_MessageIsLocalisedByAcceptLanguage()
    {
        var client = _factory.ClientFor(TestTokens.JobSeeker());
        var arabic = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/settings");
        arabic.Headers.AcceptLanguage.ParseAdd("ar");

        var ar = await (await client.SendAsync(arabic)).Json();
        var en = await (await client.GetAsync("/api/v1/admin/settings")).Json();

        ar["detail"]!.GetValue<string>().Should().Be("يمكن للمسؤولين فقط تنفيذ هذا الإجراء.");
        en["detail"]!.GetValue<string>().Should().Be("Only administrators may perform this action.");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-01")]
    [Trait("AC", "AC-01")]
    public async Task Users_Admin_GetsTheComposedList_AndFiltersApply()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var all = await (await client.GetAsync("/api/v1/admin/users")).Json();
        var employers = await (await client.GetAsync("/api/v1/admin/users?type=Employer")).Json();
        var search = await (await client.GetAsync("/api/v1/admin/users?search=layla&status=Active&pageSize=1")).Json();

        all["totalCount"]!.GetValue<int>().Should().Be(4);
        employers["items"]!.AsArray().Select(i => i!["actorType"]!.GetValue<string>()).Should().Equal("Employer");
        (search["totalCount"]!.GetValue<int>(), search["items"]!.AsArray().Count).Should().Be((1, 1));
        all["items"]![0]!["email"]!.GetValue<string>().Should().Contain("***", "the interface only ever sees masked personal data");
    }

    [Theory]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?page=0", "page")]
    [InlineData("?type=alien", "type")]
    public async Task Users_InvalidQuery_Is400WithFieldErrors(string query, string field)
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/users" + query);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
        (await response.Json())["errors"]![field].Should().NotBeNull();
    }

    [Fact]
    public async Task HealthAndOpenApi_AreAvailable()
    {
        var client = _factory.ClientFor(null);

        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        var openApi = await (await client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync();
        openApi.Should().Contain("/api/v1/admin/taxonomies/{type}/nodes").And.Contain("/internal/v1/taxonomies/{type}");
    }
}
