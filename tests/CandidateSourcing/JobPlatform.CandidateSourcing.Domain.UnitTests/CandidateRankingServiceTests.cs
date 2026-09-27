using JobPlatform.CandidateSourcing.Domain.Ranking;

namespace JobPlatform.CandidateSourcing.Domain.UnitTests;

public class CandidateRankingServiceTests
{
    [Fact]
    [Trait("Story", "US-3.3.3-02")]
    [Trait("AC", "AC-01")]
    public void Rank_OrdersByScoreDescending()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var ranked = CandidateRankingService.Rank(new[] { (a, 40m), (b, 90m), (c, 60m) });

        ranked.Select(r => r.CandidateProfileId).Should().Equal(b, c, a);
        ranked.Select(r => r.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-02")]
    [Trait("AC", "AC-02")]
    public void Rank_WithTiedScores_BreaksTiesByLowestCandidateId()
    {
        var low = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var high = Guid.Parse("00000000-0000-0000-0000-000000000002");

        var ranked = CandidateRankingService.Rank(new[] { (high, 75m), (low, 75m) });

        ranked.Select(r => r.CandidateProfileId).Should().Equal(low, high);
        ranked[0].Rank.Should().Be(1);
        ranked[1].Rank.Should().Be(2);
    }

    [Fact]
    public void Rank_IsStableAcrossRepeatedCalls()
    {
        var candidates = new[] { (Guid.NewGuid(), 50m), (Guid.NewGuid(), 50m), (Guid.NewGuid(), 50m) };

        var first = CandidateRankingService.Rank(candidates);
        var second = CandidateRankingService.Rank(candidates);

        first.Select(r => r.CandidateProfileId).Should().Equal(second.Select(r => r.CandidateProfileId));
    }

    [Theory]
    [InlineData(50, 0, 50)]
    [InlineData(0, 70, 70)]
    [InlineData(60, 60, 60)]
    public void EffectiveThreshold_IsTheHigherOfPlatformAndEmployer(decimal platform, int employer, decimal expected) =>
        EffectiveThresholdCalculator.Effective(platform, employer).Should().Be(expected);
}
