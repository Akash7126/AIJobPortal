using System.Net;
using JobPlatform.CandidateSourcing.Infrastructure.Adapters;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.TestSupport;

namespace JobPlatform.CandidateSourcing.Api.IntegrationTests;

public class RecommendationsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RecommendationsApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    [Trait("Story", "US-3.3.3-01")]
    public async Task Recommendations_ForUnknownPosting_Returns404()
    {
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.GetAsync($"/api/v1/employers/me/jobs/{Guid.NewGuid()}/candidate-recommendations");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "E-CRFE-NOT-FOUND");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-01")]
    public async Task Recommendations_ForAPostingOwnedByAnotherEmployer_Returns403()
    {
        var owningEmployerId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        FakeJobPostingApiClient.Seed(ApiFactory.Posting(jobPostingId, owningEmployerId));
        var client = _factory.ClientFor(TestTokens.Employer());

        var response = await client.GetAsync($"/api/v1/employers/me/jobs/{jobPostingId}/candidate-recommendations");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-CRFE-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-01")]
    [Trait("AC", "CS.Recommendation.EMPTY_IS_OK")]
    public async Task Recommendations_WithNoQualifyingCandidates_ReturnsAnEmptyPage()
    {
        var employerId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        FakeJobPostingApiClient.Seed(ApiFactory.Posting(jobPostingId, employerId));
        var client = _factory.ClientFor(TestTokens.Employer(employerId));

        var response = await client.GetAsync($"/api/v1/employers/me/jobs/{jobPostingId}/candidate-recommendations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["totalCount"]!.GetValue<int>().Should().Be(0);
        body["items"]!.AsArray().Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-01")]
    public async Task Recommendations_WithAQualifyingCandidate_IncludesThem()
    {
        var employerId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        FakeJobPostingApiClient.Seed(ApiFactory.Posting(jobPostingId, employerId));
        var candidateId = await _factory.PublicCandidateAsync();
        FakeAiMatchingApiClient.Seed(jobPostingId, new MatchScoreDto(Guid.NewGuid(), candidateId, jobPostingId, 82m, "v1", DateTime.UtcNow,
            new[] { new MatchCriterionDto("skills", 90m, 0.5m, true) }));
        var client = _factory.ClientFor(TestTokens.Employer(employerId));

        var response = await client.GetAsync($"/api/v1/employers/me/jobs/{jobPostingId}/candidate-recommendations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Json();
        body["items"]!.AsArray().Should().Contain(i => i!["candidateProfileId"]!.GetValue<Guid>() == candidateId);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-02")]
    [Trait("AC", "CS.Ranking.TIE_BREAK_LOWEST_ID")]
    public async Task Ranking_OrdersByScoreDescending_WithStrengthsAndGaps()
    {
        var employerId = Guid.NewGuid();
        var jobPostingId = Guid.NewGuid();
        FakeJobPostingApiClient.Seed(ApiFactory.Posting(jobPostingId, employerId));
        var strongCandidate = await _factory.PublicCandidateAsync();
        var weakCandidate = await _factory.PublicCandidateAsync();
        FakeAiMatchingApiClient.Seed(jobPostingId, new MatchScoreDto(Guid.NewGuid(), strongCandidate, jobPostingId, 90m, "v1", DateTime.UtcNow,
            new[] { new MatchCriterionDto("skills", 95m, 0.5m, true) }));
        FakeAiMatchingApiClient.Seed(jobPostingId, new MatchScoreDto(Guid.NewGuid(), weakCandidate, jobPostingId, 40m, "v1", DateTime.UtcNow,
            new[] { new MatchCriterionDto("skills", 40m, 0.5m, true) }));
        var client = _factory.ClientFor(TestTokens.Employer(employerId));

        var response = await client.GetAsync($"/api/v1/employers/me/jobs/{jobPostingId}/candidate-ranking");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Json())["items"]!.AsArray();
        items.Should().HaveCount(2);
        items[0]!["candidateProfileId"]!.GetValue<Guid>().Should().Be(strongCandidate);
        items[0]!["rank"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task Recommendations_AsJobSeeker_Returns403()
    {
        var client = _factory.ClientFor(TestTokens.JobSeeker());

        var response = await client.GetAsync($"/api/v1/employers/me/jobs/{Guid.NewGuid()}/candidate-recommendations");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "E-CRFE-FORBIDDEN");
    }
}
