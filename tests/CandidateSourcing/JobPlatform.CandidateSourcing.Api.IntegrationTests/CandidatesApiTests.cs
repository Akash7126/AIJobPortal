using System.Net;
using JobPlatform.TestSupport;

namespace JobPlatform.CandidateSourcing.Api.IntegrationTests;

public class CandidatesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CandidatesApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.3.3-04")]
    public async Task Search_AsUnverifiedEmployer_Returns403()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.PostJsonAsync("/api/v1/employers/me/candidates/search", new { skills = Array.Empty<string>() });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-CRFE-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-04")]
    public async Task Search_AsVerifiedEmployer_FindsThePublicCandidate()
    {
        var employerId = await _factory.VerifiedEmployerAsync();
        var candidateId = await _factory.PublicCandidateAsync(educationLevel: "Bachelor", locationCode: "Ramallah");
        var client = _factory.ClientFor(TestTokens.Employer(employerId));

        var response = await client.PostJsonAsync("/api/v1/employers/me/candidates/search",
            new { educationLevel = "Bachelor", locationCode = "Ramallah" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["items"]!.AsArray().Should().Contain(i => i!["candidateProfileId"]!.GetValue<Guid>() == candidateId);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-04")]
    [Trait("AC", "GAP-001")]
    public async Task Search_WithInvertedSalaryRange_Returns400()
    {
        var employerId = await _factory.VerifiedEmployerAsync();
        var client = _factory.ClientFor(TestTokens.Employer(employerId));

        var response = await client.PostJsonAsync("/api/v1/employers/me/candidates/search", new { salaryMin = 5000, salaryMax = 1000 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-05")]
    public async Task GetCandidate_Public_ReturnsTheView()
    {
        var candidateId = await _factory.PublicCandidateAsync();
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.GetAsync($"/api/v1/employers/me/candidates/{candidateId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Json())["profileId"]!.GetValue<Guid>().Should().Be(candidateId);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-05")]
    [Trait("AC", "AC-02")]
    public async Task GetCandidate_Unknown_Returns404()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.GetAsync($"/api/v1/employers/me/candidates/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-CRFE-NOT-FOUND");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    public async Task Insight_ForOwningEmployerAndVisibleCandidate_ReturnsFit()
    {
        var employerId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        JobPlatform.CandidateSourcing.Infrastructure.Adapters.FakeJobPostingApiClient.Seed(ApiFactory.Posting(jobPostingId, employerId));
        var candidateId = await _factory.PublicCandidateAsync();
        var client = _factory.ClientFor(TestTokens.Employer(employerId));

        var response = await client.GetAsync($"/api/v1/employers/me/candidates/{candidateId}/insight?jobPostingId={jobPostingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["candidateProfileId"]!.GetValue<Guid>().Should().Be(candidateId);
        body["jobPostingId"]!.GetValue<Guid>().Should().Be(jobPostingId);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    public async Task Insight_ForAnUnknownPosting_Returns404()
    {
        var candidateId = await _factory.PublicCandidateAsync();
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.GetAsync($"/api/v1/employers/me/candidates/{candidateId}/insight?jobPostingId={Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-CRFE-NOT-FOUND");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    public async Task Insight_ForAPostingOwnedByAnotherEmployer_Returns403()
    {
        var owningEmployerId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        JobPlatform.CandidateSourcing.Infrastructure.Adapters.FakeJobPostingApiClient.Seed(ApiFactory.Posting(jobPostingId, owningEmployerId));
        var candidateId = await _factory.PublicCandidateAsync();
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.GetAsync($"/api/v1/employers/me/candidates/{candidateId}/insight?jobPostingId={jobPostingId}");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-CRFE-FORBIDDEN");
    }
}
