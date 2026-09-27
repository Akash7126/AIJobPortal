using JobPlatform.CandidateSourcing.Domain.Insight;

namespace JobPlatform.CandidateSourcing.Domain.UnitTests;

public class CandidateInsightTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Employer = Guid.NewGuid();
    private static readonly Guid Posting = Guid.NewGuid();
    private static readonly Guid Candidate = Guid.NewGuid();

    private static readonly (string Criterion, decimal Score, bool Included)[] Breakdown =
    {
        ("SkillOverlap", 90m, true), ("Education", 50m, true), ("Location", 30m, true), ("Salary", 40m, false)
    };

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-01")]
    public void Compute_WithDisclosedFields_ExposesAvailabilityAndSalary()
    {
        var insight = CandidateInsight.Compute(Employer, Posting, Candidate, Employer, new[] { "availability", "salary" }, "Immediate", 3000m, 4000m,
            72m, Breakdown, At);

        insight.Availability.Should().Be("Immediate");
        insight.ExpectedSalaryMin.Should().Be(3000m);
        insight.ExpectedSalaryMax.Should().Be(4000m);
        insight.WithheldFields.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-02")]
    public void Compute_WithUndisclosedFields_ShowsUnavailableNotADefault()
    {
        var insight = CandidateInsight.Compute(Employer, Posting, Candidate, Employer, Array.Empty<string>(), "Immediate", 3000m, 4000m, 72m, Breakdown, At);

        insight.Availability.Should().BeNull();
        insight.ExpectedSalaryMin.Should().BeNull();
        insight.ExpectedSalaryMax.Should().BeNull();
        insight.WithheldFields.Should().BeEquivalentTo(new[] { "availability", "salary" });
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-03")]
    public void Compute_OnlyScoresIncludedCriteria()
    {
        var insight = CandidateInsight.Compute(Employer, Posting, Candidate, Employer, new[] { "availability", "salary" }, null, null, null, 72m,
            Breakdown, At);

        insight.Fit.Criteria.Should().HaveCount(3);
        insight.Fit.Criteria.Select(c => c.Criterion).Should().NotContain("Salary");
    }

    [Theory]
    [InlineData(75, true, false)]
    [InlineData(90, true, false)]
    [InlineData(74.99, false, false)]
    [InlineData(40, false, false)]
    [InlineData(39.99, false, true)]
    [InlineData(0, false, true)]
    public void Fit_StrengthAndGapThresholds_AreAppliedPerCriterion(decimal score, bool expectedStrength, bool expectedGap)
    {
        var fit = Fit.From(80m, new[] { ("Criterion", score) });

        var criterion = fit.Criteria.Single();
        criterion.IsStrength.Should().Be(expectedStrength);
        criterion.IsGap.Should().Be(expectedGap);
    }

    [Fact]
    public void Compute_RaisesCandidateInsightComputedDomainEvent()
    {
        var insight = CandidateInsight.Compute(Employer, Posting, Candidate, Employer, new[] { "availability" }, "Immediate", null, null, 72m, Breakdown, At);

        var e = insight.DomainEvents.Single().Should().BeOfType<CandidateInsightComputedDomainEvent>().Which;
        e.EmployerAccountId.Should().Be(Employer);
        e.CandidateProfileId.Should().Be(Candidate);
        e.JobPostingId.Should().Be(Posting);
        e.Availability.Should().Be("Immediate");
    }
}
