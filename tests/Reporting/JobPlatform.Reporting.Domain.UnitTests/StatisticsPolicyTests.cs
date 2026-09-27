using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Domain.UnitTests;

public class StatisticsPolicyTests
{
    private static readonly StatisticsPolicy Policy = new();

    [Fact]
    public void IsInsufficient_BelowMinSampleSize_ReturnsTrue() =>
        Policy.IsInsufficient(StatisticsPolicy.DefaultMinSampleSize - 1).Should().BeTrue();

    [Fact]
    public void IsInsufficient_AtMinSampleSize_ReturnsFalse() =>
        Policy.IsInsufficient(StatisticsPolicy.DefaultMinSampleSize).Should().BeFalse();

    [Fact]
    public void IsSuppressed_BelowMinCell_ReturnsTrue() =>
        Policy.IsSuppressed(StatisticsPolicy.DefaultMinCell - 1).Should().BeTrue();

    [Fact]
    public void Measure_InsufficientSample_ReturnsInsufficientFlagWithNoValue()
    {
        var measured = StatisticsCalculator.Measure(Policy, 3, () => 42);

        measured.InsufficientData.Should().BeTrue();
        measured.Value.Should().Be(0);
        measured.SampleSize.Should().Be(3);
    }

    [Fact]
    public void Measure_SufficientSample_ComputesValue()
    {
        var measured = StatisticsCalculator.Measure(Policy, 50, () => 42);

        measured.InsufficientData.Should().BeFalse();
        measured.Value.Should().Be(42);
    }

    [Theory]
    [InlineData(null, "unspecified")]
    [InlineData("", "unspecified")]
    [InlineData("  ", "unspecified")]
    [InlineData(" Ramallah ", "Ramallah")]
    public void Bucket_MissingOrBlank_FallsBackToUnspecified(string? value, string expected) =>
        StatisticsCalculator.Bucket(value).Should().Be(expected);

    [Fact]
    public void Ratio_ZeroDenominator_ReturnsNull() =>
        StatisticsCalculator.Ratio(5, 0).Should().BeNull();

    [Fact]
    public void Ratio_ComputesRoundedFraction() =>
        StatisticsCalculator.Ratio(1, 3).Should().Be(0.3333m);

    [Fact]
    public void Growth_NoPreviousPeriod_ReturnsNull() =>
        StatisticsCalculator.Growth(0, 100).Should().BeNull();

    [Fact]
    public void Growth_ComputesPercentageChange() =>
        StatisticsCalculator.Growth(100, 150).Should().Be(0.5m);

    [Fact]
    public void Midpoint_BothNull_ReturnsZero() =>
        StatisticsCalculator.Midpoint(null, null).Should().Be(0);

    [Fact]
    public void Midpoint_OnlyMin_ReturnsMin() =>
        StatisticsCalculator.Midpoint(100, null).Should().Be(100);

    [Fact]
    public void Midpoint_OnlyMax_ReturnsMax() =>
        StatisticsCalculator.Midpoint(null, 200).Should().Be(200);

    [Fact]
    public void Midpoint_BothPresent_ReturnsAverage() =>
        StatisticsCalculator.Midpoint(100, 200).Should().Be(150);
}
