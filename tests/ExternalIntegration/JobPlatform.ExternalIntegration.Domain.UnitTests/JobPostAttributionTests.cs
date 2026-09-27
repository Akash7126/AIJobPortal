using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain.UnitTests;

public class JobPostAttributionTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Actor = new(Guid.NewGuid(), false);

    private static JobPostAttribution Tagged() =>
        JobPostAttribution.Tag(Guid.NewGuid(), Guid.NewGuid(), "plat-1", "Jobs4All", "https://partner.example/1", Actor.Id, At);

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    [Trait("AC", "AC-01")]
    public void Tag_StartsActiveAndRaisesEvent()
    {
        var attribution = Tagged();

        attribution.SyncState.Should().Be(AttributionSyncState.Active);
        attribution.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JobPostAttributionUpdatedDomainEvent>()
            .Which.ToStatus.Should().Be("Active");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    [Trait("AC", "AC-02")]
    public void ExtendDeadline_WhileActive_Succeeds()
    {
        var attribution = Tagged();
        attribution.ClearDomainEvents();

        attribution.ExtendDeadline(At.AddMonths(2), Actor, At.AddDays(1));

        attribution.DeadlineUtc.Should().Be(At.AddMonths(2));
        attribution.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JobPostAttributionUpdatedDomainEvent>()
            .Which.ToStatus.Should().Be("Updated");
    }

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    [Trait("AC", "AC-02")]
    public void EditDescription_WhileActive_Succeeds()
    {
        var attribution = Tagged();

        attribution.EditDescription("Updated description.", Actor, At.AddDays(1));

        attribution.Description.Should().Be("Updated description.");
    }

    [Theory]
    [Trait("Story", "US-3.1.3-09")]
    [Trait("AC", "AC-04")]
    [InlineData("close")]
    [InlineData("deactivate")]
    [InlineData("delete")]
    public void Terminate_FromActive_TransitionsAndRaisesEvent(string operation)
    {
        var attribution = Tagged();
        attribution.ClearDomainEvents();

        switch (operation)
        {
            case "close":
                attribution.Close(Actor, At);
                attribution.SyncState.Should().Be(AttributionSyncState.Closed);
                break;
            case "deactivate":
                attribution.Deactivate(Actor, At);
                attribution.SyncState.Should().Be(AttributionSyncState.Deactivated);
                break;
            default:
                attribution.Delete(Actor, At);
                attribution.SyncState.Should().Be(AttributionSyncState.Deleted);
                break;
        }

        attribution.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    [Trait("AC", "AC-03")]
    public void ExtendDeadline_AfterClose_ThrowsStateClosed()
    {
        var attribution = Tagged();
        attribution.Close(Actor, At);

        var act = () => attribution.ExtendDeadline(At.AddMonths(1), Actor, At.AddDays(1));

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.AttributionStateClosed);
        ex.ExternalCode.Should().Be(ErrorCodes.AttributionStateClosed);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-09")]
    [Trait("AC", "AC-03")]
    public void EditDescription_AfterDelete_ThrowsStateClosed()
    {
        var attribution = Tagged();
        attribution.Delete(Actor, At);

        var act = () => attribution.EditDescription("text", Actor, At.AddDays(1));

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.AttributionStateClosed);
    }

    [Fact]
    public void Close_Twice_ThrowsNotActive()
    {
        var attribution = Tagged();
        attribution.Close(Actor, At);

        var act = () => attribution.Close(Actor, At.AddDays(1));

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.AttributionNotActive);
    }
}
