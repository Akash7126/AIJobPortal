using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Projection;

namespace JobPlatform.CandidateSourcing.Domain.UnitTests;

public class CandidateProjectionTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplyCandidateView_FirstApplication_SetsAllFields()
    {
        var projection = CandidateProjection.Create(Guid.NewGuid(), Guid.NewGuid());

        projection.ApplyCandidateView(CandidateVisibility.Public, false, false, new[] { "csharp", "sql" }, "Bachelor", 5m, "PS-RAM", 2000m, 3000m,
            "Immediate", eventVersion: 1, At);

        projection.Visibility.Should().Be(CandidateVisibility.Public);
        projection.Skills.Should().Equal("csharp", "sql");
        projection.LastEventVersion.Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-04")]
    [Trait("AC", "AC-01")]
    public void ApplyCandidateView_OutOfOrderDelivery_IsIgnored()
    {
        var projection = CandidateProjection.Create(Guid.NewGuid(), Guid.NewGuid());
        projection.ApplyCandidateView(CandidateVisibility.Public, false, false, new[] { "csharp" }, null, null, null, null, null, null, eventVersion: 5, At);

        projection.ApplyCandidateView(CandidateVisibility.Private, true, true, new[] { "stale" }, null, null, null, null, null, null, eventVersion: 3, At);

        projection.Visibility.Should().Be(CandidateVisibility.Public);
        projection.Skills.Should().Equal("csharp");
        projection.LastEventVersion.Should().Be(5);
    }

    [Fact]
    public void MarkDeactivated_ExcludesFromEmployerVisibility()
    {
        var projection = CandidateProjection.Create(Guid.NewGuid(), Guid.NewGuid());
        projection.ApplyCandidateView(CandidateVisibility.Public, false, false, Array.Empty<string>(), null, null, null, null, null, null, eventVersion: 1, At);

        projection.MarkDeactivated(At);

        projection.Deactivated.Should().BeTrue();
        CandidatePrivacyPolicy.IsVisibleToEmployers(projection.ToSnapshot(Array.Empty<string>())).Should().BeFalse();
    }
}
