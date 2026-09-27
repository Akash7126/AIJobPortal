using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class CriterionWeightsTests
{
    [Fact]
    public void Default_SumsToOneHundred() =>
        CriterionWeights.Default.Sum.Should().Be(100);

    [Fact]
    public void Create_ValidWeights_Succeeds()
    {
        var weights = CriterionWeights.Create(30, 15, 10, 15, 15, 15);

        weights.SkillOverlap.Should().Be(30);
        weights.Sum.Should().Be(100);
    }

    [Fact]
    public void Create_SumNotOneHundred_Throws()
    {
        var act = () => CriterionWeights.Create(50, 15, 10, 15, 15, 15);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(AiRuleCodes.ConfigOutOfRange);
        ex.ExternalCode.Should().Be(AiErrorCodes.InvalidField);
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }

    [Theory]
    [InlineData(-1, 15, 10, 15, 15, 15)]
    [InlineData(101, 0, 10, 15, 15, 15)]
    public void Create_WeightOutOfBounds_Throws(decimal a, decimal b, decimal c, decimal d, decimal e, decimal f)
    {
        var act = () => CriterionWeights.Create(a, b, c, d, e, f);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AiRuleCodes.ConfigOutOfRange);
    }

    [Fact]
    public void Create_SumWithinEpsilon_Succeeds() =>
        CriterionWeights.Create(30.005m, 15, 10, 15, 15, 15).Sum.Should().BeApproximately(100.005m, 0.001m);

    [Fact]
    public void Of_ReturnsThePerCriterionWeight()
    {
        var weights = CriterionWeights.Create(30, 15, 10, 15, 15, 15);

        weights.Of(Criterion.Education).Should().Be(15);
        weights.Of(Criterion.Salary).Should().Be(15);
    }

    [Fact]
    public void Equality_IsValueBased() =>
        CriterionWeights.Create(30, 15, 10, 15, 15, 15).Should().Be(CriterionWeights.Create(30, 15, 10, 15, 15, 15));
}

public class MatchScoringEngineTests
{
    private static readonly ExactSkillSimilarity Similarity = ExactSkillSimilarity.Instance;

    [Fact]
    public void Overlap_FullMatch_ReturnsOne() =>
        MatchScoringEngine.Overlap(new[] { "C#", "SQL" }, new[] { "c#", "sql", "python" }, Similarity).Should().Be(1d);

    [Fact]
    public void Overlap_PartialMatch_ReturnsFraction() =>
        MatchScoringEngine.Overlap(new[] { "C#", "SQL" }, new[] { "c#" }, Similarity).Should().Be(0.5d);

    [Fact]
    public void Overlap_EitherSideEmpty_ReturnsNull()
    {
        MatchScoringEngine.Overlap(Array.Empty<string>(), new[] { "c#" }, Similarity).Should().BeNull();
        MatchScoringEngine.Overlap(new[] { "c#" }, Array.Empty<string>(), Similarity).Should().BeNull();
    }

    [Fact]
    public void Education_ProfileMeetsOrExceedsRequirement_ReturnsOne()
    {
        MatchScoringEngine.Education(EducationLevel.Master, EducationLevel.Bachelor).Should().Be(1d);
        MatchScoringEngine.Education(EducationLevel.Bachelor, EducationLevel.Bachelor).Should().Be(1d);
    }

    [Fact]
    public void Education_ProfileBelowRequirement_Decays()
    {
        var oneLevelBelow = MatchScoringEngine.Education(EducationLevel.Secondary, EducationLevel.Diploma);
        var twoLevelsBelow = MatchScoringEngine.Education(EducationLevel.Primary, EducationLevel.Diploma);

        oneLevelBelow.Should().BeApproximately(1d - MatchScoringEngine.EducationStep, 0.0001);
        twoLevelsBelow.Should().BeLessThan(oneLevelBelow!.Value);
    }

    [Fact]
    public void Education_EitherSideMissing_ReturnsNull()
    {
        MatchScoringEngine.Education(null, EducationLevel.Bachelor).Should().BeNull();
        MatchScoringEngine.Education(EducationLevel.Bachelor, null).Should().BeNull();
    }

    private static readonly ProfileMatchView BlankProfile = new(Guid.NewGuid(), 1, Array.Empty<string>(), null, Array.Empty<string>(), null, null,
        Array.Empty<WorkArrangement>(), null, null, null);
    private static readonly PostingMatchView BlankPosting = new(Guid.NewGuid(), 1, true, Array.Empty<string>(), null, Array.Empty<string>(), null, null,
        WorkArrangement.OnSite, null, null, null, null);

    [Fact]
    public void Location_RemotePostingAndProfileAcceptsIt_ReturnsOne()
    {
        var posting = BlankPosting with { Arrangement = WorkArrangement.Remote };

        MatchScoringEngine.Location(BlankProfile, posting).Should().Be(1d);
    }

    [Fact]
    public void Location_SameCity_ReturnsOne()
    {
        var profile = BlankProfile with { City = "Ramallah" };
        var posting = BlankPosting with { City = "ramallah" };

        MatchScoringEngine.Location(profile, posting).Should().Be(1d);
    }

    [Fact]
    public void Location_SameGovernorateDifferentCity_ReturnsPartial()
    {
        var profile = BlankProfile with { City = "Al-Bireh", Governorate = "Ramallah" };
        var posting = BlankPosting with { City = "Ramallah City", Governorate = "Ramallah" };

        MatchScoringEngine.Location(profile, posting).Should().Be(MatchScoringEngine.SameGovernorateScore);
    }

    [Fact]
    public void Location_DifferentGovernorate_ReturnsZero()
    {
        var profile = BlankProfile with { City = "Gaza", Governorate = "Gaza" };
        var posting = BlankPosting with { City = "Ramallah", Governorate = "Ramallah" };

        MatchScoringEngine.Location(profile, posting).Should().Be(0d);
    }

    [Fact]
    public void Location_NoLocationDataOnEitherSide_ReturnsNull() =>
        MatchScoringEngine.Location(BlankProfile, BlankPosting).Should().BeNull();

    [Fact]
    public void Experience_WithinRange_ReturnsOne() =>
        MatchScoringEngine.Experience(3m, 1, 5).Should().Be(1d);

    [Fact]
    public void Experience_BelowMinimum_Decays() =>
        MatchScoringEngine.Experience(1m, 5, 10).Should().BeLessThan(1d);

    [Fact]
    public void Experience_AboveMaximum_DecaysAtHalfRate()
    {
        var slightlyOver = MatchScoringEngine.Experience(11m, 1, 10);

        slightlyOver.Should().BeLessThan(1d).And.BeGreaterThan(0.9d);
    }

    [Fact]
    public void Experience_MissingBothSides_ReturnsNull() =>
        MatchScoringEngine.Experience(null, null, null).Should().BeNull();

    [Fact]
    public void Salary_OfferAtOrAboveExpectation_ReturnsOne() =>
        MatchScoringEngine.Salary(1000, 1500, 1500, 2000).Should().Be(1d);

    [Fact]
    public void Salary_OfferBelowExpectation_LessThanOne()
    {
        var result = MatchScoringEngine.Salary(2000, 2500, 1000, 1500);

        result.Should().BeLessThan(1d);
    }

    [Fact]
    public void Salary_OverlappingRanges_ReturnsPartial() =>
        MatchScoringEngine.Salary(1000, 2000, 1500, 2500).Should().BeApproximately(0.5d, 0.01);

    [Fact]
    public void Salary_MissingData_ReturnsNull() =>
        MatchScoringEngine.Salary(null, null, 1000, 2000).Should().BeNull();

    [Fact]
    public void Compute_MissingCriterion_IsExcludedAndRemainingWeightsReNormalised()
    {
        // No education data on either side (profile.Education null) -> that criterion is excluded (INV-04); weights renormalise over the rest.
        var profile = new ProfileMatchView(Guid.NewGuid(), 1, new[] { "c#" }, null, Array.Empty<string>(), "Ramallah", "Ramallah",
            Array.Empty<WorkArrangement>(), 3m, null, null);
        var posting = new PostingMatchView(Guid.NewGuid(), 1, true, new[] { "c#" }, EducationLevel.Bachelor, Array.Empty<string>(), "Ramallah", "Ramallah",
            WorkArrangement.OnSite, 1, 5, null, null);

        var result = MatchScoringEngine.Instance.Compute(profile, posting, CriterionWeights.Default, ExactSkillSimilarity.Instance);

        result.Breakdown.Single(b => b.Criterion == Criterion.Education).Included.Should().BeFalse();
        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compute_AllCriteriaFullyMatched_ReturnsOneHundred()
    {
        var profile = new ProfileMatchView(Guid.NewGuid(), 1, new[] { "c#", "sql" }, EducationLevel.Bachelor, new[] { "scrum" }, "Ramallah", "Ramallah",
            new[] { WorkArrangement.OnSite }, 3m, 2000, 3000);
        var posting = new PostingMatchView(Guid.NewGuid(), 1, true, new[] { "c#", "sql" }, EducationLevel.Bachelor, new[] { "scrum" }, "Ramallah", "Ramallah",
            WorkArrangement.OnSite, 1, 5, 2000, 3000);

        var result = MatchScoringEngine.Instance.Compute(profile, posting, CriterionWeights.Default, ExactSkillSimilarity.Instance);

        result.Score.Should().Be(100);
        result.Breakdown.Should().OnlyContain(b => b.Included);
    }

    [Fact]
    public void Compute_NothingScoreable_ReturnsZero()
    {
        var result = MatchScoringEngine.Instance.Compute(BlankProfile, BlankPosting, CriterionWeights.Default, ExactSkillSimilarity.Instance);

        result.Score.Should().Be(0);
        result.Breakdown.Should().OnlyContain(b => !b.Included);
    }
}

public class MatchScoreTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly MatchingConfigSnapshot Config = MatchingConfigSnapshot.Defaults;

    private static ProfileMatchView Profile(long version = 1) =>
        new(Guid.NewGuid(), version, new[] { "c#" }, EducationLevel.Bachelor, Array.Empty<string>(), "Ramallah", "Ramallah",
            Array.Empty<WorkArrangement>(), 3m, null, null);

    private static PostingMatchView Posting(bool active = true, long version = 1) =>
        new(Guid.NewGuid(), version, active, new[] { "c#" }, EducationLevel.Bachelor, Array.Empty<string>(), "Ramallah", "Ramallah",
            WorkArrangement.OnSite, 1, 5, null, null);

    [Fact]
    public void Compute_ActivePosting_RaisesMatchScoreComputedDomainEvent()
    {
        var profile = Profile();
        var posting = Posting();

        var score = MatchScore.Compute(profile, posting, Config, ExactSkillSimilarity.Instance, "model-1", At);

        score.ProfileId.Should().Be(profile.ProfileId);
        score.JobPostingId.Should().Be(posting.PostingId);
        score.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<MatchScoreComputedDomainEvent>()
            .Which.Score.Should().Be(score.Score);
    }

    [Fact]
    public void Compute_NonActivePosting_ThrowsNonActivePosting()
    {
        var act = () => MatchScore.Compute(Profile(), Posting(active: false), Config, ExactSkillSimilarity.Instance, "model-1", At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(AiRuleCodes.NonActivePosting);
    }

    [Fact]
    public void IsCurrent_SameVersions_ReturnsTrue()
    {
        var profile = Profile();
        var posting = Posting();
        var score = MatchScore.Compute(profile, posting, Config, ExactSkillSimilarity.Instance, "model-1", At);

        score.IsCurrent(Config, profile.Version, posting.Version).Should().BeTrue();
    }

    [Fact]
    public void Recompute_SameVersions_ReturnsFalseAndRaisesNoNewEvent()
    {
        var profile = Profile();
        var posting = Posting();
        var score = MatchScore.Compute(profile, posting, Config, ExactSkillSimilarity.Instance, "model-1", At);
        score.ClearDomainEvents();

        var changed = score.Recompute(profile, posting, Config, ExactSkillSimilarity.Instance, "model-1", At);

        changed.Should().BeFalse();
        score.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Recompute_NewerProfileVersion_RecomputesAndRaisesEvent()
    {
        var posting = Posting();
        var score = MatchScore.Compute(Profile(version: 1), posting, Config, ExactSkillSimilarity.Instance, "model-1", At);
        score.ClearDomainEvents();

        var changed = score.Recompute(Profile(version: 2), posting, Config, ExactSkillSimilarity.Instance, "model-1", At.AddMinutes(1));

        changed.Should().BeTrue();
        score.ProfileVersion.Should().Be(2);
        score.DomainEvents.Should().ContainSingle();
    }
}
