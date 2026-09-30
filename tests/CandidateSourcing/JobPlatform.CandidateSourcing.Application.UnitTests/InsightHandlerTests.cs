using JobPlatform.CandidateSourcing.Application.Handlers.Insight;
using JobPlatform.CandidateSourcing.Application.Queries.Insight;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobSeekerProfile;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.Application.Interfaces.Persistence;
using JobPlatform.SharedKernel.Application.Results;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace JobPlatform.CandidateSourcing.Application.UnitTests;

public class InsightHandlerTests
{
    private readonly IJobPostingApi _postings = Substitute.For<IJobPostingApi>();
    private readonly IAiMatchingApi _matching = Substitute.For<IAiMatchingApi>();
    private readonly IJobSeekerProfileApi _profiles = Substitute.For<IJobSeekerProfileApi>();
    private readonly FakeStore _store = new();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = Kit.Clock();
    private readonly Guid _posting = Guid.NewGuid();
    private readonly Guid _candidate = Guid.NewGuid();

    private GetCandidateInsightHandler Handler() => new(_postings, _matching, _profiles, _store, _unitOfWork, Kit.User(), _clock);

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-01")]
    public async Task GetInsight_ComputesAndPersistsAgainstMatchScore()
    {
        _postings.GetPostingForMatchingAsync(_posting, Arg.Any<CancellationToken>())
            .Returns(new PostingForMatchingDto(_posting, Kit.EmployerId, "Active", false, 1, "Engineer", "it", null, Array.Empty<string>(), null,
                Array.Empty<string>(), null, null, "OnSite", null, null, null, null));
        _profiles.GetCandidateViewAsync(_candidate, Arg.Any<CancellationToken>())
            .Returns(new CandidateViewDto(_candidate, "Public", false, false, new[] { "availability" }, Array.Empty<string>(), null, null, null,
                Array.Empty<string>(), null, null, "Immediate", DateTime.UtcNow));
        _matching.ListMatchScoresAsync(_posting, null, 1, 200, Arg.Any<CancellationToken>())
            .Returns(new MatchScoreListDto(new[]
            {
                new MatchScoreDto(Guid.NewGuid(), _candidate, _posting, 82m, "v1", DateTime.UtcNow, new[] { new MatchCriterionDto("SkillOverlap", 90m, 50m, true) })
            }, 1, 200, 1, 50m, "v1"));

        var result = await Handler().Handle(new GetCandidateInsightQuery(_candidate, _posting), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Availability.Should().Be("Immediate");
        result.Value.OverallScore.Should().Be(82m);
        _store.Insights.Should().ContainSingle();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetInsight_NotOwner_ReturnsForbidden()
    {
        _postings.GetPostingForMatchingAsync(_posting, Arg.Any<CancellationToken>())
            .Returns(new PostingForMatchingDto(_posting, Guid.NewGuid(), "Active", false, 1, "Engineer", "it", null, Array.Empty<string>(), null,
                Array.Empty<string>(), null, null, "OnSite", null, null, null, null));

        var result = await Handler().Handle(new GetCandidateInsightQuery(_candidate, _posting), default);

        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetInsight_UnknownCandidate_ReturnsNotFound()
    {
        _postings.GetPostingForMatchingAsync(_posting, Arg.Any<CancellationToken>())
            .Returns(new PostingForMatchingDto(_posting, Kit.EmployerId, "Active", false, 1, "Engineer", "it", null, Array.Empty<string>(), null,
                Array.Empty<string>(), null, null, "OnSite", null, null, null, null));
        _profiles.GetCandidateViewAsync(_candidate, Arg.Any<CancellationToken>()).Returns((CandidateViewDto?)null);

        var result = await Handler().Handle(new GetCandidateInsightQuery(_candidate, _posting), default);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
