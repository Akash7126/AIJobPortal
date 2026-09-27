using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain.UnitTests;

public class MatchingConfigurationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), ActorType.Administrator);
    private static readonly Actor NonAdmin = new(Guid.NewGuid(), ActorType.JobSeeker);

    [Fact]
    public void CreateDefault_SeedsDefaultsAndOneHistoryEntry()
    {
        var config = MatchingConfiguration.CreateDefault(At);

        config.Id.Should().Be(MatchingConfiguration.SingletonId);
        config.ConfigVersion.Should().Be(1);
        config.MatchThresholdPercent.Should().Be(MatchingConfiguration.DefaultThreshold);
        config.ShortlistSize.Should().Be(MatchingConfiguration.DefaultShortlistSize);
        config.History.Should().ContainSingle();
    }

    [Fact]
    public void ChangeThreshold_ByAdministrator_UpdatesAndBumpsVersion()
    {
        var config = MatchingConfiguration.CreateDefault(At);

        config.ChangeThreshold(75, Admin, At.AddMinutes(1));

        config.MatchThresholdPercent.Should().Be(75);
        config.ConfigVersion.Should().Be(2);
        config.History.Should().HaveCount(2);
        config.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<MatchingConfigurationChangedDomainEvent>()
            .Which.ConfigVersion.Should().Be(2);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ChangeThreshold_OutOfRange_ThrowsInvalidField(decimal percent)
    {
        var config = MatchingConfiguration.CreateDefault(At);

        var act = () => config.ChangeThreshold(percent, Admin, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(AiRuleCodes.ConfigOutOfRange);
        ex.ExternalCode.Should().Be(AiErrorCodes.InvalidField);
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }

    [Fact]
    public void ChangeThreshold_ByNonAdministrator_ThrowsForbidden()
    {
        var config = MatchingConfiguration.CreateDefault(At);

        var act = () => config.ChangeThreshold(50, NonAdmin, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(AiRuleCodes.ConfigAdminOnly);
        ex.ExternalCode.Should().Be(AiErrorCodes.Forbidden);
        ex.Kind.Should().Be(BusinessRuleKind.Forbidden);
    }

    [Fact]
    public void ChangeWeights_ByAdministrator_UpdatesAndBumpsVersion()
    {
        var config = MatchingConfiguration.CreateDefault(At);
        var weights = CriterionWeights.Create(50, 10, 10, 10, 10, 10);

        config.ChangeWeights(weights, Admin, At.AddMinutes(1));

        config.Weights.Should().Be(weights);
        config.ConfigVersion.Should().Be(2);
    }

    [Fact]
    public void ChangeWeights_ByNonAdministrator_ThrowsForbidden()
    {
        var config = MatchingConfiguration.CreateDefault(At);

        var act = () => config.ChangeWeights(CriterionWeights.Default, NonAdmin, At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(AiRuleCodes.ConfigAdminOnly);
    }

    [Fact]
    public void Snapshot_ReflectsCurrentState()
    {
        var config = MatchingConfiguration.CreateDefault(At);
        config.ChangeThreshold(80, Admin, At.AddMinutes(1));

        var snapshot = config.Snapshot();

        snapshot.ConfigVersion.Should().Be(2);
        snapshot.MatchThresholdPercent.Should().Be(80);
    }

    [Fact]
    public void MidRunSnapshot_UnaffectedByLaterChange()
    {
        var config = MatchingConfiguration.CreateDefault(At);
        var snapshotBeforeChange = config.Snapshot();

        config.ChangeThreshold(90, Admin, At.AddMinutes(1));

        snapshotBeforeChange.MatchThresholdPercent.Should().Be(MatchingConfiguration.DefaultThreshold);
        snapshotBeforeChange.ConfigVersion.Should().Be(1);
    }
}
