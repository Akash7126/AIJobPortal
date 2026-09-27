using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Domain.Common;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.EmployerOnboarding.Domain.UnitTests;

public class EmployerRegistrationTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static EmployerRegistration OpenRegistration(Guid? employerId = null) =>
        EmployerRegistration.OpenFor(Guid.NewGuid(), employerId ?? Guid.NewGuid(), At);

    private static Level2Details ValidLevel2() =>
        new("https://acme.example", "Software", CompanySize.Small, new Address("Ramallah", "Ramallah", "Main Street"), "A small software company.");

    private static CompanyIdentity ValidIdentity() => new("Acme Ltd", "CO-123", "REG-456");

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    public void OpenFor_StartsPending()
    {
        var registration = OpenRegistration();

        registration.Status.Should().Be(RegistrationStatus.Pending);
        registration.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    public void SubmitLevel2_ByOwner_WhilePending_Succeeds()
    {
        var employerId = Guid.NewGuid();
        var registration = OpenRegistration(employerId);

        registration.SubmitLevel2(ValidIdentity(), ValidLevel2(), new Actor(employerId, false));

        registration.Level2.Should().NotBeNull();
        registration.CompanyIdentity.Should().NotBeNull();
    }

    [Fact]
    public void SubmitLevel2_ByNonOwner_ThrowsForbidden()
    {
        var registration = OpenRegistration();

        var act = () => registration.SubmitLevel2(ValidIdentity(), ValidLevel2(), new Actor(Guid.NewGuid(), false));

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.RegistrationOwnerOnly);
        ex.ExternalCode.Should().Be("E-ERPM-FORBIDDEN", "employer self-service owner-only refusals share the ERPM family; ADMIN_ONLY is the only AUM-FORBIDDEN case");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-03")]
    public void SubmitLevel2_AfterApproval_ThrowsNotPending()
    {
        var employerId = Guid.NewGuid();
        var registration = OpenRegistration(employerId);
        registration.SubmitLevel2(ValidIdentity(), ValidLevel2(), new Actor(employerId, false));
        registration.Approve(Admin, At);

        var act = () => registration.SubmitLevel2(ValidIdentity(), ValidLevel2(), new Actor(employerId, false));

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(RuleCodes.RegistrationNotPending);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-04")]
    public void Approve_WithoutSubmittedLevel2_ThrowsProfileNotSubmitted()
    {
        var registration = OpenRegistration();

        var act = () => registration.Approve(Admin, At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.RegistrationProfileNotSubmitted);
        ex.ExternalCode.Should().Be("E-AUM-PROFILE-NOT-SUBMITTED");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-01")]
    public void Approve_WithSubmittedLevel2_TransitionsToApprovedAndRaisesEvent()
    {
        var employerId = Guid.NewGuid();
        var registration = OpenRegistration(employerId);
        registration.SubmitLevel2(ValidIdentity(), ValidLevel2(), new Actor(employerId, false));

        registration.Approve(Admin, At.AddMinutes(5));

        registration.Status.Should().Be(RegistrationStatus.Approved);
        registration.ApprovedBy.Should().Be(Admin.Id);
        registration.ApprovedAtUtc.Should().Be(At.AddMinutes(5));
        registration.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployerRegistrationApprovedDomainEvent>()
            .Which.EmployerAccountId.Should().Be(employerId);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-02")]
    public void Approve_Twice_ThrowsAlreadyApproved()
    {
        var employerId = Guid.NewGuid();
        var registration = OpenRegistration(employerId);
        registration.SubmitLevel2(ValidIdentity(), ValidLevel2(), new Actor(employerId, false));
        registration.Approve(Admin, At);

        var act = () => registration.Approve(Admin, At.AddMinutes(1));

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.RegistrationAlreadyApproved);
        ex.ExternalCode.Should().Be("E-AUM-STATE-APPROVED");
    }

    [Fact]
    [Trait("Story", "US-3.1.4-04")]
    [Trait("AC", "AC-03")]
    public void Approve_ByNonAdministrator_ThrowsForbidden()
    {
        var employerId = Guid.NewGuid();
        var registration = OpenRegistration(employerId);
        registration.SubmitLevel2(ValidIdentity(), ValidLevel2(), new Actor(employerId, false));

        var act = () => registration.Approve(new Actor(Guid.NewGuid(), false), At);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(RuleCodes.RegistrationAdminOnly);
        ex.ExternalCode.Should().Be("E-AUM-FORBIDDEN");
    }
}
