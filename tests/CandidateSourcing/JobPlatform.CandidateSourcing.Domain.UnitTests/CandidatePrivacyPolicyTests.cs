using JobPlatform.CandidateSourcing.Domain.Privacy;

namespace JobPlatform.CandidateSourcing.Domain.UnitTests;

public class CandidatePrivacyPolicyTests
{
    private static CandidateSnapshot Snapshot(CandidateVisibility visibility, bool optIn, bool deactivated, params string[] disclosed) =>
        new(Guid.NewGuid(), visibility, optIn, deactivated, disclosed);

    // CS.Privacy.* (US-3.3.3-05, US-3.3.3-07 AC-04): the full visibility matrix - visibility x opt-in x deactivated.
    [Theory]
    [Trait("Story", "US-3.3.3-05")]
    [Trait("AC", "AC-01")]
    [InlineData(CandidateVisibility.Public, false, false, true)] // public, not deactivated -> visible
    [InlineData(CandidateVisibility.Public, true, false, true)] // public + opted in -> visible
    [InlineData(CandidateVisibility.Public, false, true, false)] // public but deactivated -> excluded
    [InlineData(CandidateVisibility.Public, true, true, false)] // public + opted in but deactivated -> excluded
    [InlineData(CandidateVisibility.Private, false, false, false)] // private, no opt-in -> excluded
    [InlineData(CandidateVisibility.Private, true, false, true)] // private but opted in -> visible (BC-11 Q-07)
    [InlineData(CandidateVisibility.Private, false, true, false)] // private + deactivated -> excluded
    [InlineData(CandidateVisibility.Private, true, true, false)] // private + opted in but deactivated -> excluded (deactivation always wins)
    public void IsVisibleToEmployers_FullMatrix(CandidateVisibility visibility, bool optIn, bool deactivated, bool expected)
    {
        var snapshot = Snapshot(visibility, optIn, deactivated);

        CandidatePrivacyPolicy.IsVisibleToEmployers(snapshot).Should().Be(expected);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-06")]
    [Trait("AC", "AC-02")]
    public void IsFieldDisclosed_IsCaseInsensitiveAndOnlyMatchesListedFields()
    {
        var snapshot = Snapshot(CandidateVisibility.Public, false, false, "Skills", "Location");

        CandidatePrivacyPolicy.IsFieldDisclosed(snapshot, "skills").Should().BeTrue();
        CandidatePrivacyPolicy.IsFieldDisclosed(snapshot, "salary").Should().BeFalse();
    }
}
