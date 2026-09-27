using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.GovernmentIntegration.Api.IntegrationTests;

public class AdminApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AdminApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.4.2-02")]
    [Trait("AC", "AC-04")]
    public async Task ConfigureConnection_ByAdministrator_Succeeds_AndIsListed()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());

        var configure = await admin.PutJsonAsync("/api/v1/admin/government-connections/MoL",
            new { endpoint = "https://mol.example/api", authMethod = "ApiKey", credentialRef = "secret-ref", enabled = true });

        configure.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await (await admin.GetAsync("/api/v1/admin/government-connections")).Json();
        list.AsArray().Should().Contain(c => c!["source"]!.GetValue<string>() == "MoL");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-02")]
    [Trait("AC", "AC-04")]
    public async Task ConfigureConnection_ByNonAdministrator_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).PutJsonAsync("/api/v1/admin/government-connections/PEF",
            new { endpoint = "https://pef.example/api", authMethod = "ApiKey", credentialRef = "ref", enabled = true });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-GI-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.4.2-02")]
    public async Task ConfigureConnection_NonHttpsEndpoint_Is400()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).PutJsonAsync("/api/v1/admin/government-connections/MoL",
            new { endpoint = "http://insecure.example", authMethod = "ApiKey", credentialRef = "ref", enabled = true });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VAL.INVALID_REQUEST");
    }

    [Fact]
    [Trait("Story", "US-6.1-01")]
    [Trait("AC", "AC-04")]
    public async Task StartMigration_ByAdministrator_Returns202()
    {
        var admin = _factory.ClientFor(TestTokens.Admin());

        var response = await admin.PostJsonAsync("/api/v1/admin/migrations", new { phases = new[] { "Import", "Cleanse" }, dryRun = false });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var body = await response.Json();
        body["status"]!.GetValue<string>().Should().Be("Running");

        var id = body["id"]!.GetValue<Guid>();
        var get = await (await admin.GetAsync($"/api/v1/admin/migrations/{id}")).Json();
        get["phases"]!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    [Trait("Story", "US-6.1-01")]
    [Trait("AC", "AC-04")]
    public async Task StartMigration_ByNonAdministrator_Is403()
    {
        var response = await _factory.ClientFor(TestTokens.Employer()).PostJsonAsync("/api/v1/admin/migrations",
            new { phases = new[] { "Import" }, dryRun = false });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-GI-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-6.1-04")]
    public async Task GetDataQuality_UnknownBatch_Is404()
    {
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync($"/api/v1/admin/data-quality/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-GI-NOT-FOUND");
    }
}
