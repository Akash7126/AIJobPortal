using System.Net;

namespace JobPlatform.AccountIdentity.Api.IntegrationTests;

public class SmokeTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SmokeTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_Live_And_Ready_ReturnHealthy()
    {
        var client = new ApiClient(_factory);

        (await client.Http.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.Http.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ThenActivate_ThenLogin_ReturnsTokens()
    {
        var client = new ApiClient(_factory);
        var mobile = ApiClient.NewMobile();
        var id = await client.ActiveJobSeekerAsync(mobile);

        var login = await client.LoginAsync(mobile, "Str0ngPass");

        login["status"]!.GetValue<string>().Should().Be("Authenticated");
        id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Administrator_CanCompleteMfaFlow()
    {
        var admin = await new ApiClient(_factory).AdminAsync();

        (await admin.Http.GetAsync("/api/v1/admin/password-policy")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
