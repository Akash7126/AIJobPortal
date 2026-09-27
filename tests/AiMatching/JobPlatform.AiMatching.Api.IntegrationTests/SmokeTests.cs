using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.AiMatching.Api.IntegrationTests;

public class HealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_Live_ReturnsOk()
    {
        var response = await _factory.ClientFor(null).GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public class MatchingConfigurationApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MatchingConfigurationApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.ClientFor(null).GetAsync("/api/v1/admin/matching-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AsJobSeeker_ReturnsForbidden()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync("/api/v1/admin/matching-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AsAdministrator_ReturnsTheConfigurationWithAnETag()
    {
        // Other tests in this fixture mutate the shared configuration, so this only asserts shape/authorization, not the seeded default value.
        var response = await _factory.ClientFor(TestTokens.Admin()).GetAsync("/api/v1/admin/matching-configuration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.ETag.Should().NotBeNull();
        var body = await response.Json();
        body["matchThresholdPercent"]!.GetValue<decimal>().Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task PutThreshold_WithStaleIfMatch_ReturnsPreconditionFailed()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var response = await client.PutJsonAsync("/api/v1/admin/matching-configuration/threshold", new { thresholdPercent = 70 }, ifMatch: "\"stale\"");

        response.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task PutThreshold_OutOfRange_ReturnsValidationProblem()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var response = await client.PutJsonAsync("/api/v1/admin/matching-configuration/threshold", new { thresholdPercent = 150 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutThreshold_Valid_UpdatesConfiguration()
    {
        var client = _factory.ClientFor(TestTokens.Admin());

        var putResponse = await client.PutJsonAsync("/api/v1/admin/matching-configuration/threshold", new { thresholdPercent = 72 });
        putResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync("/api/v1/admin/matching-configuration");
        (await getResponse.Json())["matchThresholdPercent"]!.GetValue<decimal>().Should().Be(72);
    }
}

public class MatchesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public MatchesApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task JobRanking_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.ClientFor(null).GetAsync("/api/v1/matches/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JobRanking_AsJobSeeker_ReturnsOk()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync("/api/v1/matches/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InternalMatchRanking_WithoutServiceToken_ReturnsForbidden()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync($"/internal/v1/match-ranking?profileId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InternalMatchRanking_WithServiceToken_ReturnsOk()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/match-ranking?profileId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
