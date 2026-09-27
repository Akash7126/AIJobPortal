using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.ExternalIntegration.Api.IntegrationTests;

public class PartnerApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PartnerApiTests(ApiFactory factory) => _factory = factory;

    private static object RegisterBody(bool recommended = true) => new
    {
        sourcePlatformName = "Jobs4All-" + Guid.NewGuid().ToString("N")[..6], baseUrl = "https://jobs4all.example", recommendedByMolPef = recommended
    };

    private async Task<HttpClient> RegisteredAndActiveAsync(Guid partnerId, bool recommended = true)
    {
        await _factory.IngestAsync(ApiFactory.PartnerAccountApproved(partnerId));
        var partner = _factory.ClientFor(TestTokens.Partner(partnerId));
        var register = await partner.PostJsonAsync("/api/v1/partner/integrations/register", RegisterBody(recommended));
        register.StatusCode.Should().Be(HttpStatusCode.Created);

        if (!recommended)
        {
            var integrationId = (await register.Json())["integrationId"]!.GetValue<Guid>();
            var admin = _factory.ClientFor(TestTokens.Admin());
            await admin.PostJsonAsync($"/api/v1/admin/integrations/{integrationId}/approve", new { approvalBasis = "Manual MoL review" });
            await admin.PostJsonAsync($"/api/v1/admin/integrations/{integrationId}/activate");
        }
        else
        {
            var integrationId = (await register.Json())["integrationId"]!.GetValue<Guid>();
            await _factory.ClientFor(TestTokens.Admin()).PostJsonAsync($"/api/v1/admin/integrations/{integrationId}/activate");
        }

        return partner;
    }

    [Fact]
    [Trait("Story", "US-3.4.1-01")]
    [Trait("AC", "AC-01")]
    public async Task Register_Recommended_ThenActivate_AndEnable_PublishesSupportedEvent()
    {
        var partnerId = Guid.NewGuid();
        var partner = await RegisteredAndActiveAsync(partnerId);

        var enable = await partner.PutJsonAsync("/api/v1/partner/integration/models", new { pullEnabled = false, pushEnabled = true });
        enable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.PublishAsync();
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "external-job-site-integration.supported.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-01")]
    public async Task PushJob_ThenRePush_Returns201ThenOk_AndPublishesJobDataImported()
    {
        var partnerId = Guid.NewGuid();
        var partner = await RegisteredAndActiveAsync(partnerId);
        await partner.PutJsonAsync("/api/v1/partner/integration/models", new { pullEnabled = false, pushEnabled = true });

        var body = new
        {
            sourceJobId = "job-1", title = "Backend Engineer", summary = "Build things.", skills = new[] { "C#", "SQL" }, contractType = "FullTime",
            workFormat = "Remote", applicationDeadline = DateTime.UtcNow.AddMonths(1), location = "Ramallah", sourceUrl = "https://jobs4all.example/1"
        };
        var first = await partner.PostJsonAsync("/api/v1/partner/jobs", body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var platformJobId = (await first.Json())["platformJobId"]!.GetValue<string>();

        var second = await partner.PostJsonAsync("/api/v1/partner/jobs", body with { });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Json())["platformJobId"]!.GetValue<string>().Should().Be(platformJobId);

        await _factory.PublishAsync();
        _factory.Bus.Messages.Should().Contain(m => m.RoutingKey == "job-data.imported.v1");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-03")]
    [Trait("AC", "AC-03")]
    public async Task PushJob_MissingTitle_Is400()
    {
        var partnerId = Guid.NewGuid();
        var partner = await RegisteredAndActiveAsync(partnerId);
        await partner.PutJsonAsync("/api/v1/partner/integration/models", new { pullEnabled = false, pushEnabled = true });

        var response = await partner.PostJsonAsync("/api/v1/partner/jobs", new
        {
            sourceJobId = "job-1", title = "", summary = "Build things.", skills = new[] { "C#" }, contractType = "FullTime", workFormat = "Remote",
            applicationDeadline = DateTime.UtcNow.AddMonths(1), location = "Ramallah", sourceUrl = (string?)null
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    [Trait("AC", "AC-03")]
    public async Task SyncAttribution_Close_ThenEditAgain_Is409()
    {
        var partnerId = Guid.NewGuid();
        var partner = await RegisteredAndActiveAsync(partnerId);
        await partner.PutJsonAsync("/api/v1/partner/integration/models", new { pullEnabled = false, pushEnabled = true });
        var push = await partner.PostJsonAsync("/api/v1/partner/jobs", new
        {
            sourceJobId = "job-close", title = "Dev", summary = "Summary", skills = new[] { "C#" }, contractType = "FullTime", workFormat = "Remote",
            applicationDeadline = DateTime.UtcNow.AddMonths(1), location = "Ramallah", sourceUrl = (string?)null
        });
        var platformJobId = (await push.Json())["platformJobId"]!.GetValue<string>();

        var close = await partner.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/partner/jobs/{platformJobId}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { operation = "Close", deadline = (DateTime?)null, description = (string?)null })
        });
        close.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var editAfterClose = await partner.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/partner/jobs/{platformJobId}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new { operation = "EditDescription", deadline = (DateTime?)null, description = "new" })
        });

        await editAfterClose.ShouldBeProblemAsync(HttpStatusCode.Conflict, "E-TPJPRI-STATE-CLOSED");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-04")]
    public async Task ConfigureMapping_ThenGet_RoundTrips()
    {
        var partnerId = Guid.NewGuid();
        var partner = await RegisteredAndActiveAsync(partnerId);

        var configure = await partner.PutJsonAsync("/api/v1/partner/mapping", new
        {
            rules = new[]
            {
                new { sourceField = "t", targetField = "title", transform = "None" }, new { sourceField = "s", targetField = "summary", transform = "None" },
                new { sourceField = "sk", targetField = "skills", transform = "SplitComma" }
            },
            standardSchemaVersion = "v1"
        });
        configure.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await partner.GetAsync("/api/v1/partner/mapping");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        (await get.Json())["version"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-04")]
    [Trait("AC", "AC-02")]
    public async Task ConfigureMapping_MissingRequiredField_Is422()
    {
        var partnerId = Guid.NewGuid();
        var partner = await RegisteredAndActiveAsync(partnerId);

        var response = await partner.PutJsonAsync("/api/v1/partner/mapping", new
        {
            rules = new[] { new { sourceField = "t", targetField = "title", transform = "None" } }, standardSchemaVersion = "v1"
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "E-TPJPRI-REQUIRED-FIELD");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-05")]
    [Trait("AC", "AC-02")]
    public async Task ProvisionSandbox_WithoutCredential_Is403()
    {
        var partnerId = Guid.NewGuid();
        var partner = await RegisteredAndActiveAsync(partnerId);

        var response = await partner.PostJsonAsync("/api/v1/partner/sandbox");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-TPJPRI-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-05")]
    [Trait("AC", "AC-01")]
    public async Task ProvisionSandbox_WithActiveCredential_Succeeds()
    {
        var partnerId = Guid.NewGuid();
        var partner = await RegisteredAndActiveAsync(partnerId);
        await _factory.IngestAsync(ApiFactory.PartnerCredentialCreated(partnerId, DateTime.UtcNow.AddDays(30)));

        var response = await partner.PostJsonAsync("/api/v1/partner/sandbox");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetIntegration_Anonymous_Is401()
    {
        var response = await _factory.ClientFor(null).GetAsync("/api/v1/partner/integration");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_ByJobSeeker_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).PostJsonAsync("/api/v1/partner/integrations/register", RegisterBody());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
