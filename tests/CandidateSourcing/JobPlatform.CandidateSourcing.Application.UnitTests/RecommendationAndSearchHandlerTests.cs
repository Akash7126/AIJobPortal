using JobPlatform.CandidateSourcing.Application.Recommendations;
using JobPlatform.CandidateSourcing.Application.Search;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Projection;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Results;
using NSubstitute;

namespace JobPlatform.CandidateSourcing.Application.UnitTests;

public class RecommendationAndSearchHandlerTests
{
    private readonly FakeStore _store = new();
    private readonly FakeCache _cache = new();
    private readonly IJobPostingApi _postings = Substitute.For<IJobPostingApi>();
    private readonly IAiMatchingApi _matching = Substitute.For<IAiMatchingApi>();
    private readonly Guid _posting = Guid.NewGuid();

    private static PostingForMatchingDto Posting(Guid id, Guid employer) =>
        new(id, employer, "Active", false, 1, "Engineer", "it", null, Array.Empty<string>(), null, Array.Empty<string>(), null, null, "OnSite", null, null, null, null);

    private void OwnedByCaller() => _postings.GetPostingForMatchingAsync(_posting, Arg.Any<CancellationToken>()).Returns(Posting(_posting, Kit.EmployerId));

    private void Scores(params MatchScoreDto[] scores) =>
        _matching.ListMatchScoresAsync(_posting, null, 1, 200, Arg.Any<CancellationToken>()).Returns(new MatchScoreListDto(scores, 1, 200, scores.Length, 50m, "v1"));

    private void VisibleCandidate(Guid candidateId)
    {
        var projection = CandidateProjection.Create(candidateId, Guid.NewGuid());
        projection.ApplyCandidateView(CandidateVisibility.Public, false, false, Array.Empty<string>(), null, null, null, null, null, null, 1, DateTime.UtcNow);
        _store.Projections.Add(projection);
    }

    private CandidateQualificationService Service() => new(_postings, _matching, _store, _store, _cache);

    [Fact]
    [Trait("Story", "US-3.3.3-01")]
    [Trait("AC", "AC-02")]
    public async Task Recommendations_NoQualifyingCandidates_ReturnsEmptyNotError()
    {
        OwnedByCaller();
        Scores();
        var handler = new GetCandidateRecommendationsHandler(Service(), Kit.User());

        var result = await handler.Handle(new GetCandidateRecommendationsQuery(_posting, 1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-01")]
    [Trait("AC", "AC-01")]
    public async Task Recommendations_NotOwner_ReturnsForbidden()
    {
        _postings.GetPostingForMatchingAsync(_posting, Arg.Any<CancellationToken>()).Returns(Posting(_posting, Guid.NewGuid()));
        var handler = new GetCandidateRecommendationsHandler(Service(), Kit.User());

        var result = await handler.Handle(new GetCandidateRecommendationsQuery(_posting, 1, 20), default);

        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Recommendations_UnknownPosting_ReturnsNotFound()
    {
        var handler = new GetCandidateRecommendationsHandler(Service(), Kit.User());

        var result = await handler.Handle(new GetCandidateRecommendationsQuery(Guid.NewGuid(), 1, 20), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-05")]
    [Trait("AC", "AC-02")]
    public async Task Recommendations_ExcludesPrivateNonOptedInCandidates()
    {
        OwnedByCaller();
        var visible = Guid.NewGuid();
        VisibleCandidate(visible);
        var privateCandidate = Guid.NewGuid();
        var privateProjection = CandidateProjection.Create(privateCandidate, Guid.NewGuid());
        privateProjection.ApplyCandidateView(CandidateVisibility.Private, false, false, Array.Empty<string>(), null, null, null, null, null, null, 1, DateTime.UtcNow);
        _store.Projections.Add(privateProjection);
        Scores(
            new MatchScoreDto(Guid.NewGuid(), visible, _posting, 90m, "v1", DateTime.UtcNow, Array.Empty<MatchCriterionDto>()),
            new MatchScoreDto(Guid.NewGuid(), privateCandidate, _posting, 95m, "v1", DateTime.UtcNow, Array.Empty<MatchCriterionDto>()));

        var handler = new GetCandidateRecommendationsHandler(Service(), Kit.User());
        var result = await handler.Handle(new GetCandidateRecommendationsQuery(_posting, 1, 20), default);

        result.Value.Items.Select(i => i.CandidateProfileId).Should().Equal(visible);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-02")]
    [Trait("AC", "AC-01")]
    public async Task Ranking_ReturnsStrengthsAndGapsFromBreakdown()
    {
        OwnedByCaller();
        var candidate = Guid.NewGuid();
        VisibleCandidate(candidate);
        Scores(new MatchScoreDto(Guid.NewGuid(), candidate, _posting, 80m, "v1", DateTime.UtcNow,
            new[] { new MatchCriterionDto("SkillOverlap", 90m, 50m, true), new MatchCriterionDto("Location", 20m, 10m, true) }));
        var handler = new GetCandidateRankingHandler(Service(), Kit.User());

        var result = await handler.Handle(new GetCandidateRankingQuery(_posting, 1, 20), default);

        var item = result.Value.Items.Single();
        item.Rank.Should().Be(1);
        item.Strengths.Should().Contain("SkillOverlap");
        item.Gaps.Should().Contain("Location");
    }

    [Fact]
    [Trait("Story", "US-3.3.3-04")]
    [Trait("AC", "AC-03")]
    public async Task Search_UnverifiedEmployer_ReturnsForbidden()
    {
        var handler = new SearchCandidateDatabaseHandler(_store, _store, Kit.User());

        var result = await handler.Handle(new SearchCandidateDatabaseQuery(new CandidateSearchCriteria(null, null, null, null, null, null, null, null), 1, 20), default);

        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Search_VerifiedEmployer_ReturnsResults()
    {
        _store.VerifiedEmployers.Add(VerifiedEmployer.Create(Kit.EmployerId, DateTime.UtcNow));
        VisibleCandidate(Guid.NewGuid());
        var handler = new SearchCandidateDatabaseHandler(_store, _store, Kit.User());

        var result = await handler.Handle(new SearchCandidateDatabaseQuery(new CandidateSearchCriteria(null, null, null, null, null, null, null, null), 1, 20), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-05")]
    [Trait("AC", "AC-03")]
    public async Task GetCandidateView_PrivateAndNotOptedIn_ReturnsNotFound()
    {
        var profiles = Substitute.For<IJobSeekerProfileApi>();
        var candidateId = Guid.NewGuid();
        profiles.GetCandidateViewAsync(candidateId, Arg.Any<CancellationToken>())
            .Returns(new CandidateViewDto(candidateId, "Private", false, false, Array.Empty<string>(), Array.Empty<string>(), null, null, null,
                Array.Empty<string>(), null, null, null, DateTime.UtcNow));
        var handler = new GetCandidateViewHandler(profiles);

        var result = await handler.Handle(new GetCandidateViewQuery(candidateId), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
