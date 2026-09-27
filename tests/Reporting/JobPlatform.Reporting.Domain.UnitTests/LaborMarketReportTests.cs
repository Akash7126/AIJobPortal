using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class LaborMarketReportTests
{
    private static readonly DateTime At = new(2026, 3, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TryParsePeriod_ValidMonth_ReturnsFirstAndLastInstant()
    {
        var ok = LaborMarketReport.TryParsePeriod("2026-03", out var start, out var end);

        ok.Should().BeTrue();
        start.Should().Be(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("not-a-period")]
    [InlineData("2026")]
    public void TryParsePeriod_Invalid_ReturnsFalse(string period) =>
        LaborMarketReport.TryParsePeriod(period, out _, out _).Should().BeFalse();

    [Fact]
    public void PeriodOf_FormatsAsYyyyMm() =>
        LaborMarketReport.PeriodOf(At).Should().Be("2026-03");

    [Fact]
    public void Generate_ValidPastPeriod_Succeeds()
    {
        var report = LaborMarketReport.Generate("2026-02", "{}", Guid.NewGuid(), At);

        report.Period.Should().Be("2026-02");
        report.RetainUntilUtc.Should().Be(At.AddMonths(LaborMarketReport.RetentionMonths));
    }

    [Fact]
    public void Generate_FuturePeriod_Throws()
    {
        var act = () => LaborMarketReport.Generate("2026-06", "{}", Guid.NewGuid(), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(ReportingRuleCodes.InvalidPeriod);
        ex.ExternalCode.Should().Be(ReportingErrorCodes.EmploymentInvalidField);
    }

    [Fact]
    public void Generate_MalformedPeriod_Throws()
    {
        var act = () => LaborMarketReport.Generate("not-a-period", "{}", Guid.NewGuid(), At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.InvalidPeriod);
    }

    [Fact]
    public void Generate_CurrentMonth_IsAllowed()
    {
        var report = LaborMarketReport.Generate("2026-03", "{}", Guid.NewGuid(), At);

        report.Period.Should().Be("2026-03");
    }
}
