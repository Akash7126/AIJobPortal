using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class JobRecommendationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Compute_NoCollaborativeData_UsesContentOnlyStrategy()
    {
        var job = Guid.NewGuid();
        var candidates = new[] { new ScoredPosting(job, 80, 0) };

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, new Dictionary<Guid, decimal>(), thresholdPercent: 60, maxItems: 10,
            Guid.NewGuid(), At);

        recommendation.Strategy.Should().Be(RecommendationStrategy.ContentOnly);
        recommendation.Items.Single().Score.Should().Be(80);
    }

    [Fact]
    public void Compute_WithCollaborativeData_UsesHybridStrategyAndBlendsScores()
    {
        var job = Guid.NewGuid();
        var candidates = new[] { new ScoredPosting(job, 80, 0) };
        var collaborative = new Dictionary<Guid, decimal> { [job] = 40 };

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, collaborative, thresholdPercent: 0, maxItems: 10, Guid.NewGuid(), At);

        recommendation.Strategy.Should().Be(RecommendationStrategy.Hybrid);
        var expected = Math.Round(JobRecommendation.ContentWeight * 80 + (1 - JobRecommendation.ContentWeight) * 40, 2);
        recommendation.Items.Single().Score.Should().Be(expected);
    }

    [Fact]
    public void Compute_BelowThreshold_IsFilteredOut()
    {
        var candidates = new[] { new ScoredPosting(Guid.NewGuid(), 50, 0) };

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, new Dictionary<Guid, decimal>(), thresholdPercent: 60, maxItems: 10,
            Guid.NewGuid(), At);

        recommendation.Items.Should().BeEmpty();
    }

    [Fact]
    public void Compute_OrdersByScoreDescendingThenIdAscending()
    {
        var lowerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var candidates = new[]
        {
            new ScoredPosting(higherId, 70, 0),
            new ScoredPosting(lowerId, 70, 0),
            new ScoredPosting(Guid.NewGuid(), 90, 0)
        };

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, new Dictionary<Guid, decimal>(), 0, 10, Guid.NewGuid(), At);

        recommendation.Items[0].Score.Should().Be(90);
        recommendation.Items[1].JobPostingId.Should().Be(lowerId);
        recommendation.Items[2].JobPostingId.Should().Be(higherId);
    }

    [Fact]
    public void Compute_RespectsMaxItems()
    {
        var candidates = Enumerable.Range(0, 5).Select(i => new ScoredPosting(Guid.NewGuid(), 60 + i, 0)).ToArray();

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, new Dictionary<Guid, decimal>(), 0, maxItems: 2, Guid.NewGuid(), At);

        recommendation.Items.Should().HaveCount(2);
    }

    [Fact]
    public void Compute_ZeroMaxItems_Throws()
    {
        var act = () => JobRecommendation.Compute(Guid.NewGuid(), Array.Empty<ScoredPosting>(), new Dictionary<Guid, decimal>(), 0, 0, Guid.NewGuid(), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AiRuleCodes.InvalidInput);
    }

    [Fact]
    public void Compute_EventCarriesOnlyTopTenJobIds()
    {
        var candidates = Enumerable.Range(0, 15).Select(i => new ScoredPosting(Guid.NewGuid(), 50 + i, 0)).ToArray();

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, new Dictionary<Guid, decimal>(), 0, maxItems: 15, Guid.NewGuid(), At);

        var evt = recommendation.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JobRecommendationComputedDomainEvent>().Which;
        evt.TopJobIds.Should().HaveCount(10);
    }

    [Fact]
    public void Compute_HighPreferenceFit_MarksReasonAsPreference()
    {
        var candidates = new[] { new ScoredPosting(Guid.NewGuid(), 70, PreferenceFit: 1m) };

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, new Dictionary<Guid, decimal>(), 0, 10, Guid.NewGuid(), At);

        recommendation.Items.Single().Reason.Should().Be(RecommendationReason.Preference);
    }

    [Fact]
    public void Compute_HybridWithHigherCollaborativeScore_MarksReasonAsCollaborative()
    {
        var job = Guid.NewGuid();
        var candidates = new[] { new ScoredPosting(job, 30, 0) };
        var collaborative = new Dictionary<Guid, decimal> { [job] = 90 };

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, collaborative, 0, 10, Guid.NewGuid(), At);

        recommendation.Items.Single().Reason.Should().Be(RecommendationReason.Collaborative);
    }

    [Fact]
    public void Compute_DefaultReason_IsContent()
    {
        var candidates = new[] { new ScoredPosting(Guid.NewGuid(), 70, PreferenceFit: 0m) };

        var recommendation = JobRecommendation.Compute(Guid.NewGuid(), candidates, new Dictionary<Guid, decimal>(), 0, 10, Guid.NewGuid(), At);

        recommendation.Items.Single().Reason.Should().Be(RecommendationReason.Content);
    }
}
