using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class ActivityLogRetentionPolicyTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateDefault_UsesTwelveMonthsWhenLegalMinimumIsLower()
    {
        var policy = ActivityLogRetentionPolicy.CreateDefault(6, At);

        policy.RetentionMonths.Should().Be(12);
        policy.LegalMinimumMonths.Should().Be(6);
    }

    [Fact]
    public void CreateDefault_UsesLegalMinimumWhenHigherThanTwelve()
    {
        var policy = ActivityLogRetentionPolicy.CreateDefault(18, At);

        policy.RetentionMonths.Should().Be(18);
    }

    [Fact]
    public void Set_AtOrAboveLegalMinimum_Succeeds()
    {
        var policy = ActivityLogRetentionPolicy.CreateDefault(12, At);

        policy.Set(24, Guid.NewGuid(), At.AddDays(1));

        policy.RetentionMonths.Should().Be(24);
    }

    [Fact]
    public void Set_BelowLegalMinimum_Throws()
    {
        var policy = ActivityLogRetentionPolicy.CreateDefault(12, At);

        var act = () => policy.Set(6, Guid.NewGuid(), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(ReportingRuleCodes.RetentionBelowLegalMinimum);
        ex.ExternalCode.Should().Be(ReportingErrorCodes.ActivityInvalidField);
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }

    [Fact]
    public void Set_AboveMax_Throws()
    {
        var policy = ActivityLogRetentionPolicy.CreateDefault(12, At);

        var act = () => policy.Set(121, Guid.NewGuid(), At);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void CutoffUtc_SubtractsRetentionMonths()
    {
        var policy = ActivityLogRetentionPolicy.CreateDefault(12, At);

        policy.CutoffUtc(At).Should().Be(At.AddMonths(-12));
    }

    [Fact]
    public void UpdateLegalMinimum_RaisesRetentionWhenBelowNewMinimum()
    {
        var policy = ActivityLogRetentionPolicy.CreateDefault(12, At);

        policy.UpdateLegalMinimum(18);

        policy.RetentionMonths.Should().Be(18);
        policy.LegalMinimumMonths.Should().Be(18);
    }

    [Fact]
    public void UpdateLegalMinimum_KeepsHigherExistingRetention()
    {
        var policy = ActivityLogRetentionPolicy.CreateDefault(12, At);
        policy.Set(36, Guid.NewGuid(), At);

        policy.UpdateLegalMinimum(18);

        policy.RetentionMonths.Should().Be(36);
    }
}
