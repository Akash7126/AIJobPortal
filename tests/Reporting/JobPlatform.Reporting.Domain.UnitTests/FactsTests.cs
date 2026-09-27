using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class ActorKeysTests
{
    [Fact]
    public void Pseudonymise_NullActor_ReturnsAnonymous() =>
        ActorKeys.Pseudonymise(null, "salt").Should().Be("anonymous");

    [Fact]
    public void Pseudonymise_EmptyActor_ReturnsAnonymous() =>
        ActorKeys.Pseudonymise(Guid.Empty, "salt").Should().Be("anonymous");

    [Fact]
    public void Pseudonymise_SameActorAndSalt_IsDeterministic()
    {
        var id = Guid.NewGuid();

        ActorKeys.Pseudonymise(id, "salt").Should().Be(ActorKeys.Pseudonymise(id, "salt"));
    }

    [Fact]
    public void Pseudonymise_NeverContainsTheRawId()
    {
        var id = Guid.NewGuid();

        ActorKeys.Pseudonymise(id, "salt").Should().NotContain(id.ToString("N"));
    }

    [Fact]
    public void Pseudonymise_DifferentSalt_ProducesDifferentKey()
    {
        var id = Guid.NewGuid();

        ActorKeys.Pseudonymise(id, "salt-a").Should().NotBe(ActorKeys.Pseudonymise(id, "salt-b"));
    }
}

public class FactEventTests
{
    private static readonly DateTime At = DateTime.UtcNow;

    [Fact]
    public void Record_Valid_Succeeds()
    {
        var fact = FactEvent.Record(Guid.NewGuid(), "job-posting", "JobPostingCreated", ActivityTypes.JobPosting, At, "Employer", "key1", "posting-1");

        fact.SourceBc.Should().Be("job-posting");
        fact.ActivityType.Should().Be(ActivityTypes.JobPosting);
    }

    [Fact]
    public void Record_EmptyMessageId_Throws()
    {
        var act = () => FactEvent.Record(Guid.Empty, "job-posting", "JobPostingCreated", ActivityTypes.JobPosting, At, "Employer", "key1", null);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.InvalidFact);
    }
}

public class FactJobPostingTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("expired", true)]
    [InlineData("archived", true)]
    [InlineData("closed", true)]
    [InlineData("active", false)]
    [InlineData("draft", false)]
    public void IsTerminal_ClassifiesStatus(string status, bool expected) =>
        FactJobPosting.IsTerminal(status).Should().Be(expected);

    [Fact]
    public void ApplyDetails_FirstCall_SetsDetailsAndStatus()
    {
        var posting = FactJobPosting.Open(Guid.NewGuid(), At);

        posting.ApplyDetails("active", "Developer", "IT", "Ramallah", 1000, 2000, "Employer", At, 1);

        posting.Title.Should().Be("Developer");
        posting.HasDetails.Should().BeTrue();
        posting.Status.Should().Be("active");
    }

    [Fact]
    public void ApplyDetails_SecondCall_NeverOverwritesDetails()
    {
        var posting = FactJobPosting.Open(Guid.NewGuid(), At);
        posting.ApplyDetails("active", "Developer", "IT", "Ramallah", 1000, 2000, "Employer", At, 1);

        posting.ApplyDetails("active", "Different title", "HR", "Gaza", 500, 900, "External", At.AddDays(1), 2);

        posting.Title.Should().Be("Developer");
        posting.Category.Should().Be("IT");
    }

    [Fact]
    public void ApplyStatus_OutOfOrder_IsIgnored()
    {
        var posting = FactJobPosting.Open(Guid.NewGuid(), At);
        posting.ApplyStatus("active", At, version: 5);

        posting.ApplyStatus("paused", At.AddMinutes(1), version: 3);

        posting.Status.Should().Be("active");
    }

    [Fact]
    public void ApplyStatus_ToTerminal_SetsClosedAt()
    {
        var posting = FactJobPosting.Open(Guid.NewGuid(), At);

        posting.ApplyStatus("expired", At, version: 1);

        posting.ClosedAtUtc.Should().Be(At);
    }

    [Fact]
    public void ApplyStatus_ReopenedAfterTerminal_ClearsClosedAt()
    {
        var posting = FactJobPosting.Open(Guid.NewGuid(), At);
        posting.ApplyStatus("expired", At, version: 1);

        posting.ApplyStatus("active", At.AddDays(1), version: 2);

        posting.ClosedAtUtc.Should().BeNull();
    }
}

public class FactOutcomeTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void HasFollowUp_NoFollowUps_IsFalse() =>
        FactOutcome.ForPosting(Guid.NewGuid(), At).HasFollowUp.Should().BeFalse();

    [Fact]
    public void FollowUp_AccumulatesFitAverage()
    {
        var outcome = FactOutcome.ForPosting(Guid.NewGuid(), At);

        outcome.FollowUp(0.8m, At);
        outcome.FollowUp(0.6m, At.AddDays(1));

        outcome.HasFollowUp.Should().BeTrue();
        outcome.FitCount.Should().Be(2);
        outcome.FitSum.Should().Be(1.4m);
    }

    [Fact]
    public void FollowUp_WithoutFit_StillCountsFollowUp()
    {
        var outcome = FactOutcome.ForPosting(Guid.NewGuid(), At);

        outcome.FollowUp(null, At);

        outcome.HasFollowUp.Should().BeTrue();
        outcome.FitCount.Should().Be(0);
    }

    [Fact]
    public void Shortlist_TracksFirstAndCount()
    {
        var outcome = FactOutcome.ForPosting(Guid.NewGuid(), At);

        outcome.Shortlist(At.AddDays(1));
        outcome.Shortlist(At.AddDays(-1));

        outcome.Shortlisted.Should().Be(2);
        outcome.FirstShortlistedAtUtc.Should().Be(At.AddDays(-1));
    }
}

public class AggDailyTests
{
    [Fact]
    public void Add_AccumulatesCount()
    {
        var agg = AggDaily.For(DateOnly.FromDateTime(DateTime.UtcNow), "event.JobPostingCreated");

        agg.Add(3);
        agg.Add(2);

        agg.Count.Should().Be(5);
    }
}

public class FactSystemMetricTests
{
    [Fact]
    public void Sample_UnknownMetric_Throws()
    {
        var act = () => FactSystemMetric.Sample("not-a-metric", 1, DateTime.UtcNow, "otel");

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.InvalidFact);
    }

    [Fact]
    public void Sample_KnownMetric_Succeeds()
    {
        var fact = FactSystemMetric.Sample(PerformanceMetrics.ErrorRatePercent, 2.5m, DateTime.UtcNow, "otel");

        fact.Value.Should().Be(2.5m);
    }
}
