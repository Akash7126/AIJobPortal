using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.JobSeekerProfile.Api.IntegrationTests;

public class InternalApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public InternalApiTests(ApiFactory factory) => _factory = factory;

    private async Task<Guid> CreateProfileAsync()
    {
        var client = _factory.ClientFor(await _factory.ActiveJobSeekerAsync());
        var created = await (await client.PostJsonAsync("/api/v1/profiles", ApiFactory.CreateProfileBody())).Json();
        return created["profileId"]!.GetValue<Guid>();
    }

    [Fact]
    public async Task GetForMatching_WithoutServiceToken_Returns401()
    {
        var response = await _factory.ClientFor(null).GetAsync($"/internal/v1/profiles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetForMatching_WithAJobSeekerToken_Returns403()
    {
        var response = await _factory.ClientFor(TestTokens.JobSeeker()).GetAsync($"/internal/v1/profiles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    [Trait("Story", "US-3.3.1-01")]
    public async Task GetForMatching_WithServiceToken_ReturnsTheProfile()
    {
        var profileId = await CreateProfileAsync();

        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/profiles/{profileId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["profileId"]!.GetValue<Guid>().Should().Be(profileId);
    }

    [Fact]
    public async Task GetForMatching_UnknownProfile_Returns404()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/profiles/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPrivacy_WithServiceToken_ReturnsTheSetting()
    {
        var profileId = await CreateProfileAsync();

        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/profiles/{profileId}/privacy");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["visibility"]!.GetValue<string>().Should().Be("Private");
    }

    [Fact]
    public async Task GetCandidateView_WithServiceToken_ReturnsTheView()
    {
        var profileId = await CreateProfileAsync();

        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/profiles/{profileId}/candidate-view");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["profileId"]!.GetValue<Guid>().Should().Be(profileId);
    }

    [Fact]
    public async Task GetResumeContentUrl_UnknownResume_Returns404()
    {
        var response = await _factory.ClientFor(TestTokens.Service()).GetAsync($"/internal/v1/resumes/{Guid.NewGuid()}/content-url");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
