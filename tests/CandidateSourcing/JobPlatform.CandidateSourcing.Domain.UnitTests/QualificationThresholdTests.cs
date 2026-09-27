using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.CandidateSourcing.Domain.Threshold;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.CandidateSourcing.Domain.UnitTests;

public class QualificationThresholdTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Employer = Guid.NewGuid();
    private static readonly Guid Posting = Guid.NewGuid();

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    [Trait("Story", "US-3.3.3-03")]
    [Trait("AC", "AC-01")]
    public void Set_WithinRange_Succeeds(int percent)
    {
        var threshold = QualificationThreshold.Open(Employer, Posting, At);

        threshold.Set(percent, Employer, At);

        threshold.Percent.Should().Be(percent);
        threshold.ThresholdVersion.Should().Be(1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [Trait("Story", "US-3.3.3-03")]
    [Trait("AC", "AC-02")]
    public void Set_OutOfRange_Throws(int percent)
    {
        var threshold = QualificationThreshold.Open(Employer, Posting, At);

        var act = () => threshold.Set(percent, Employer, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.ThresholdOutOfRange);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-03")]
    [Trait("AC", "AC-03")]
    public void Set_ByAnotherEmployer_ThrowsForbidden()
    {
        var threshold = QualificationThreshold.Open(Employer, Posting, At);

        var act = () => threshold.Set(50, Guid.NewGuid(), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.Forbidden);
    }

    [Fact]
    [Trait("Story", "US-3.3.3-03")]
    [Trait("AC", "AC-04")]
    public void Set_TwiceInARow_BumpsVersionEachTime()
    {
        var threshold = QualificationThreshold.Open(Employer, Posting, At);

        threshold.Set(40, Employer, At);
        threshold.Set(60, Employer, At);

        threshold.Percent.Should().Be(60);
        threshold.ThresholdVersion.Should().Be(2);
    }
}
