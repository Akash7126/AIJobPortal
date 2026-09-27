using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.CandidateSourcing.Api.IntegrationTests;

public class ThresholdApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ThresholdApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.3.3-03")]
    public async Task Get_WithoutOneSet_ReturnsZero()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.GetAsync($"/api/v1/employers/me/jobs/{Guid.NewGuid()}/qualification-threshold");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["percent"]!.GetValue<int>().Should().Be(0);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-03")]
    public async Task Set_ThenGet_RoundTrips()
    {
        var employerId = Guid.NewGuid();
        var client = _factory.ClientFor(TestTokens.Employer(employerId));
        var jobPostingId = Guid.NewGuid();

        var set = await client.PutJsonAsync($"/api/v1/employers/me/jobs/{jobPostingId}/qualification-threshold", new { percent = 75 });

        set.StatusCode.Should().Be(HttpStatusCode.OK);
        (await set.Json())["percent"]!.GetValue<int>().Should().Be(75);
        var get = await client.GetAsync($"/api/v1/employers/me/jobs/{jobPostingId}/qualification-threshold");
        (await get.Json())["percent"]!.GetValue<int>().Should().Be(75);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-03")]
    public async Task Set_OutOfRange_Returns400()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.PutJsonAsync($"/api/v1/employers/me/jobs/{Guid.NewGuid()}/qualification-threshold", new { percent = 150 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_AsJobSeeker_Returns403()
    {
        var client = _factory.ClientFor(TestTokens.JobSeeker());

        var response = await client.GetAsync($"/api/v1/employers/me/jobs/{Guid.NewGuid()}/qualification-threshold");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-CRFE-FORBIDDEN");
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await _factory.ClientFor(null).GetAsync($"/api/v1/employers/me/jobs/{Guid.NewGuid()}/qualification-threshold");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
