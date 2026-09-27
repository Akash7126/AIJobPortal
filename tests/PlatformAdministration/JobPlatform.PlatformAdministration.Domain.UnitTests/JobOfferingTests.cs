using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Common;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.PlatformAdministration.Domain.UnitTests;

public class JobOfferingTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static JobOffering Offering() => JobOffering.Register(Guid.NewGuid(), Guid.NewGuid(), "Engineer", At);

    [Fact]
    public void Register_StartsActive_AndRaisesNoEvent()
    {
        var offering = Offering();

        offering.Status.Should().Be(JobOfferingStatus.Active);
        offering.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-09")]
    [Trait("AC", "AC-01")]
    public void Suspend_MakesItInactiveAndRaisesSuspendedEvent()
    {
        var offering = Offering();

        offering.Suspend(Admin, " Misleading salary ", At);

        offering.Status.Should().Be(JobOfferingStatus.Inactive);
        offering.Moderation.Should().Be(ModerationKind.Suspended);
        offering.SuspendedBy.Should().Be(Admin.Id);
        offering.Reason.Should().Be("Misleading salary");
        var e = offering.DomainEvents.Single().Should().BeOfType<JobOfferingSuspendedDomainEvent>().Which;
        (e.JobOfferingId, e.ActorId, e.Reason).Should().Be((offering.Id, Admin.Id, "Misleading salary"));
    }

    [Fact]
    [Trait("Story", "US-3.1.4-09")]
    [Trait("AC", "AC-02")]
    public void Suspend_WhenAlreadyInactive_ThrowsStateInactive()
    {
        var offering = Offering();
        offering.Suspend(Admin, "first reason", At);

        var act = () => offering.Suspend(Admin, "second reason", At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.OfferingAlreadyInactive);
        ex.ExternalCode.Should().Be("E-AUM-STATE-INACTIVE");
        ex.Kind.Should().Be(BusinessRuleKind.Conflict);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-09")]
    [Trait("AC", "AC-03")]
    public void Suspend_ByNonAdministrator_ThrowsForbidden()
    {
        var act = () => Offering().Suspend(new Actor(Guid.NewGuid(), false), "reason here", At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-AUM-FORBIDDEN");
    }

    [Fact]
    public void Suspend_WithoutReason_ThrowsReasonRequired()
    {
        var act = () => Offering().Suspend(Admin, "  ", At);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.OfferingReasonRequired);
    }

    [Fact]
    public void Remove_RecordsRemovalWithTheSamePublishedEvent()
    {
        var offering = Offering();

        offering.Remove(Admin, "Policy violation", At);

        offering.Moderation.Should().Be(ModerationKind.Removed);
        offering.DomainEvents.Single().Should().BeOfType<JobOfferingSuspendedDomainEvent>().Which.Kind.Should().Be(ModerationKind.Removed);
    }

    [Fact]
    public void Remove_AfterSuspension_ThrowsStateInactive()
    {
        var offering = Offering();
        offering.Suspend(Admin, "first reason", At);

        var act = () => offering.Remove(Admin, "second reason", At);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-AUM-STATE-INACTIVE");
    }
}
