using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class LegacyDataTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private static LegacyData Imported() =>
        LegacyData.Import(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), SourceSystem.MoL, "SRC-1", "JobSeeker", "{\"name\":\"Sara\"}");

    [Fact]
    [Trait("Story", "US-6.1-02")]
    [Trait("AC", "AC-02")]
    public void FullPipeline_MapTransformValidate_ReachesValidatedStage()
    {
        var legacy = Imported();

        legacy.MapToNewSchema("{\"mapped\":true}");
        legacy.Transform("{\"mapped\":true,\"transformed\":true}");
        legacy.Validate(Array.Empty<string>());

        legacy.Stage.Should().Be(LegacyStage.Validated);
    }

    [Fact]
    [Trait("Story", "US-6.1-02")]
    [Trait("AC", "AC-03")]
    public void Validate_WithErrors_SetsInvalidAndRecordsErrors()
    {
        var legacy = Imported();
        legacy.MapToNewSchema("{}");
        legacy.Transform("{}");

        legacy.Validate(new[] { Domain.Common.ErrorCodes.LegacyInvalidField });

        legacy.Stage.Should().Be(LegacyStage.Invalid);
        legacy.ValidationErrors.Should().Contain(Domain.Common.ErrorCodes.LegacyInvalidField);
    }

    [Fact]
    [Trait("Story", "US-6.1-02")]
    [Trait("AC", "AC-04")]
    public void MarkMigrated_OnlyAfterValidated_RaisesLegacyDataImported()
    {
        var legacy = Imported();
        legacy.MapToNewSchema("{}");
        legacy.Transform("{}");
        legacy.Validate(Array.Empty<string>());

        legacy.MarkMigrated(At);

        legacy.Stage.Should().Be(LegacyStage.Migrated);
        legacy.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<LegacyDataImportedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-6.1-02")]
    [Trait("AC", "AC-04")]
    public void MarkMigrated_WithoutValidation_ThrowsNotFullyMapped()
    {
        var legacy = Imported();

        var act = () => legacy.MarkMigrated(At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.LegacyNotFullyMapped);
        ex.ExternalCode.Should().Be("E-GI-NOT-FULLY-MAPPED");
    }

    [Fact]
    public void MapToNewSchema_WhenNotImported_ThrowsStageRule()
    {
        var legacy = Imported();
        legacy.MapToNewSchema("{}");

        var act = () => legacy.MapToNewSchema("{}");

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
