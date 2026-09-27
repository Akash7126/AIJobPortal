using JobPlatform.Reporting.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class PerformanceAlertRuleTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static PerformanceAlertRule Rule(AlertComparator comparator = AlertComparator.GreaterThan, decimal threshold = 500) =>
        PerformanceAlertRule.Create(PerformanceMetrics.ResponseTimeP95Ms, comparator, threshold, 5, AlertSeverity.Warning, true, At);

    [Fact]
    public void Create_UnknownMetric_Throws()
    {
        var act = () => PerformanceAlertRule.Create("not-a-metric", AlertComparator.GreaterThan, 1, 5, AlertSeverity.Info, true, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(ReportingRuleCodes.InvalidAlertRule);
        ex.ExternalCode.Should().Be(ReportingErrorCodes.PerformanceInvalidField);
    }

    [Fact]
    public void Create_WindowBelowOneMinute_Throws()
    {
        var act = () => PerformanceAlertRule.Create(PerformanceMetrics.ErrorRatePercent, AlertComparator.GreaterThan, 1, 0, AlertSeverity.Info, true, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(ReportingRuleCodes.InvalidAlertRule);
    }

    [Theory]
    [InlineData(AlertComparator.GreaterThan, 100, 101, true)]
    [InlineData(AlertComparator.GreaterThan, 100, 100, false)]
    [InlineData(AlertComparator.GreaterThanOrEqual, 100, 100, true)]
    [InlineData(AlertComparator.LessThan, 100, 99, true)]
    [InlineData(AlertComparator.LessThan, 100, 100, false)]
    [InlineData(AlertComparator.LessThanOrEqual, 100, 100, true)]
    public void IsCrossedBy_EvaluatesTheComparator(AlertComparator comparator, decimal threshold, decimal value, bool expected) =>
        Rule(comparator, threshold).IsCrossedBy(value).Should().Be(expected);

    [Fact]
    public void Evaluate_InNormalRange_ReturnsNull() =>
        Rule(threshold: 500).Evaluate(300, At).Should().BeNull();

    [Fact]
    public void Evaluate_Crossed_RaisesAlert()
    {
        var alert = Rule(threshold: 500).Evaluate(600, At);

        alert.Should().NotBeNull();
        alert!.Value.Should().Be(600);
        alert.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PerformanceAlertRaisedDomainEvent>();
    }

    [Fact]
    public void Evaluate_DisabledRule_NeverFires()
    {
        var rule = PerformanceAlertRule.Create(PerformanceMetrics.ErrorRatePercent, AlertComparator.GreaterThan, 1, 5, AlertSeverity.Critical, false, At);

        rule.Evaluate(1000, At).Should().BeNull();
    }
}

public class PerformanceAlertTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Raise_SetsRetentionTwelveMonthsOut()
    {
        var rule = PerformanceAlertRule.Create(PerformanceMetrics.CpuUtilisationPercent, AlertComparator.GreaterThan, 90, 5, AlertSeverity.Critical, true, At);

        var alert = PerformanceAlert.Raise(rule, 95, At);

        alert.RetainUntilUtc.Should().Be(At.AddMonths(PerformanceAlert.RetentionMonths));
        alert.Metric.Should().Be(PerformanceMetrics.CpuUtilisationPercent);
        alert.Severity.Should().Be(AlertSeverity.Critical);
    }
}
