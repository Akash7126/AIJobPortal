using FluentValidation.TestHelper;
using JobPlatform.AiMatching.Application.Commands.Configuration;
using JobPlatform.AiMatching.Application.Validators.Configuration;

namespace JobPlatform.AiMatching.Application.UnitTests;

public class ConfigureMatchThresholdValidatorTests
{
    private readonly ConfigureMatchThresholdValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(100)]
    public void Valid_Passes(decimal percent) =>
        _validator.TestValidate(new ConfigureMatchThresholdCommand(percent)).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void OutOfRange_Fails(decimal percent) =>
        _validator.TestValidate(new ConfigureMatchThresholdCommand(percent)).Errors.Should().Contain(e => e.ErrorCode == "VAL.ThresholdPercent.OutOfRange");
}

public class ConfigureMatchingParameterValidatorTests
{
    private readonly ConfigureMatchingParameterValidator _validator = new();

    [Fact]
    public void ValidWeightsSummingTo100_Passes() =>
        _validator.TestValidate(new ConfigureMatchingParameterCommand(30, 15, 10, 15, 15, 15)).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void SumNotOneHundred_Fails() =>
        _validator.TestValidate(new ConfigureMatchingParameterCommand(50, 15, 10, 15, 15, 15)).Errors.Should().Contain(e => e.ErrorCode == "VAL.Weights.SumNot100");

    [Fact]
    public void WeightOutOfRange_Fails() =>
        _validator.TestValidate(new ConfigureMatchingParameterCommand(-5, 15, 10, 15, 15, 15)).Errors.Should().Contain(e => e.ErrorCode == "VAL.Weight.OutOfRange");
}
