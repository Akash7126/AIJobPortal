using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.GovernmentIntegration.Domain.UnitTests;

public class DataQualityTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Story", "US-6.1-04")]
    [Trait("AC", "AC-02")]
    public void Record_AccumulatesCounters()
    {
        var dataQuality = DataQuality.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "v1");

        dataQuality.Record(2, 1, 3, 10, 1);
        dataQuality.Record(1, 0, 0, 5, 0);

        dataQuality.IssuesResolved.Should().Be(3);
        dataQuality.DuplicatesRemoved.Should().Be(1);
        dataQuality.FormatsStandardized.Should().Be(3);
        dataQuality.RecordsChecked.Should().Be(15);
        dataQuality.RecordsRejected.Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-6.1-04")]
    [Trait("AC", "AC-02")]
    public void Complete_AfterRecording_RaisesDataQualityUpdated()
    {
        var dataQuality = DataQuality.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "v1");
        dataQuality.Record(1, 1, 1, 10, 0);

        dataQuality.Complete(At);

        dataQuality.Status.Should().Be(DataQualityStatus.Completed);
        var evt = dataQuality.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DataQualityUpdatedDomainEvent>().Which;
        evt.FromStatus.Should().Be("Running");
        evt.ToStatus.Should().Be("Completed");
    }

    [Fact]
    [Trait("Story", "US-6.1-04")]
    [Trait("AC", "AC-03")]
    public void Complete_WithoutRecording_ThrowsSnapshotIsolation()
    {
        var dataQuality = DataQuality.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "v1");

        var act = () => dataQuality.Complete(At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.DataQualitySnapshotIsolation);
        ex.ExternalCode.Should().Be("E-GI-SNAPSHOT-ISOLATION");
    }

    [Fact]
    [Trait("Story", "US-6.1-04")]
    [Trait("AC", "AC-03")]
    public void Complete_Twice_ThrowsSnapshotIsolation()
    {
        var dataQuality = DataQuality.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "v1");
        dataQuality.Record(1, 1, 1, 1, 0);
        dataQuality.Complete(At);

        var act = () => dataQuality.Complete(At);

        act.Should().Throw<BusinessRuleViolationException>();
    }
}
