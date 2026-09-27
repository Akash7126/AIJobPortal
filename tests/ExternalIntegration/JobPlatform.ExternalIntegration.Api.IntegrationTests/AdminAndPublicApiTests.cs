using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.ExternalIntegration.Api.IntegrationTests;

public class AdminApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.4.3-01")]
    public async Task ReleaseApiVersion_ThenDeprecate_ThenRetireBeforeSunset_Is409()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var version = "v" + Random.Shared.Next(1000, 9999);

        var release = await admin.PostJsonAsync("/api/v1/admin/api-versions", new { version });
        release.StatusCode.Should().Be(HttpStatusCode.Created);

        var deprecate = await admin.PostJsonAsync($"/api/v1/admin/api-versions/{version}/deprecate",
            new { sunsetAtUtc = DateTime.UtcNow.AddDays(120) });
        deprecate.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var retire = await admin.PostJsonAsync($"/api/v1/admin/api-versions/{version}/retire");
        await retire.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-APIF-RETIRE-BEFORE-SUNSET");
    }

    [Fact]
    [Trait("Story", "US-3.4.3-02")]
    [Trait("AC", "AC-04")]
    public async Task ConfigureFormat_WithUnsupportedFormat_Is422()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var version = "v" + Random.Shared.Next(1000, 9999);
        await admin.PostJsonAsync("/api/v1/admin/api-versions", new { version });

        var response = await admin.PutJsonAsync($"/api/v1/admin/api-versions/{version}/format", new { formats = new[] { "soap" } });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-APIF-UNSUPPORTED-FORMAT");
    }

    [Fact]
    public async Task ReleaseApiVersion_ByPartner_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Partner()).PostJsonAsync("/api/v1/admin/api-versions", new { version = "v99" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Story", "US-4.3-01")]
    [Trait("AC", "AC-05")]
    public async Task RegisterSoftwareInterface_Idempotent()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var body = new { category = "EmailSmsGateway", name = "SMTP-" + Guid.NewGuid().ToString("N")[..6], endpoint = "https://smtp.example" };

        var first = await admin.PutJsonAsync("/api/v1/admin/software-interfaces", body);
        var second = await admin.PutJsonAsync("/api/v1/admin/software-interfaces", body);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public class PublicApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PublicApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.4.3-03")]
    [Trait("AC", "AC-03")]
    public async Task GetDocumentation_Anonymous_Succeeds_AndTwoVersionsServedConcurrently()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var v1 = "v" + Random.Shared.Next(10000, 19999);
        var v2 = "v" + Random.Shared.Next(20000, 29999);
        await admin.PostJsonAsync("/api/v1/admin/api-versions", new { version = v1 });
        await admin.PostJsonAsync("/api/v1/admin/api-versions", new { version = v2 });

        var anon = _factory.ClientFor(null);
        var doc1 = await anon.GetAsync($"/api/v1/docs/{v1}");
        var doc2 = await anon.GetAsync($"/api/v1/docs/{v2}");

        doc1.StatusCode.Should().Be(HttpStatusCode.OK);
        doc2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-05")]
    [Trait("AC", "AC-02")]
    public async Task GetDocumentation_ForDeprecatedVersion_Succeeds_WithDeprecationHeader()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());
        var version = "v" + Random.Shared.Next(30000, 39999);
        await admin.PostJsonAsync("/api/v1/admin/api-versions", new { version });
        await admin.PostJsonAsync($"/api/v1/admin/api-versions/{version}/deprecate", new { sunsetAtUtc = DateTime.UtcNow.AddDays(120) });

        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/docs/{version}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Deprecation").Should().BeTrue();
    }

    [Fact]
    public async Task GetDocumentation_UnknownVersion_Is404()
    {
        var response = await _factory.ClientFor(null).GetAsync("/api/v1/docs/v-does-not-exist");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-EI-NOT-FOUND");
    }
}

public class InternalApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InternalApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetIntegrationSummary_ByService_Succeeds()
    {
        var partnerId = Guid.NewGuid();
        await _factory.IngestAsync(ApiFactory.PartnerAccountApproved(partnerId));
        var partner = _factory.ClientFor(TestTokens.Partner(partnerId));
        var register = await partner.PostJsonAsync("/api/v1/partner/integrations/register",
            new { sourcePlatformName = "Jobs4All", baseUrl = "https://jobs4all.example", recommendedByMolPef = true });
        var sourcePlatformId = (await register.Json())["sourcePlatformId"]!.GetValue<Guid>();

        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/integrations/{sourcePlatformId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetIntegrationSummary_ByPartner_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Partner()).GetAsync($"/internal/v1/integrations/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
