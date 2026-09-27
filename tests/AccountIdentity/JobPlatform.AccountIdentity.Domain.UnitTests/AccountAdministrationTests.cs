using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.UnitTests;

public class AccountAdministrationTests
{
    // -------------------------------------------------------------- partner approval (staff)

    [Fact]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-04")]
    public void Account_ApproveByStaff_WithAuthorisedStaff_ActivatesPartner()
    {
        var clock = TestKit.Clock();
        var partner = TestKit.Pending(ActorType.ExternalJobSite, clock);
        var staff = TestKit.Staff();

        partner.ApproveByStaff(staff, clock);

        partner.Standing.Should().Be(AccountStanding.Active);
        var raised = partner.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountActivatedDomainEvent>().Which;
        raised.ActorType.Should().Be(ActorType.ExternalJobSite);
        raised.ActorId.Should().Be(staff.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Story", "US-3.1.3-01")]
    [Trait("AC", "AC-04")]
    public void Account_ApproveByStaff_WithoutStaffAuthority_ThrowsForbidden(bool isAdmin)
    {
        var partner = TestKit.Pending(ActorType.ExternalJobSite);
        var actor = new Actor(Guid.NewGuid(), isAdmin, IsAuthorisedStaff: false);

        var act = () => partner.ApproveByStaff(actor, TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.ApproverNotAuthorised, ErrorCodes.AdminForbidden, BusinessRuleKind.Forbidden);
        partner.Standing.Should().Be(AccountStanding.Pending);
    }

    [Fact]
    public void Account_ApproveByStaff_OnNonPartner_ThrowsNotApplicable()
    {
        var jobSeeker = TestKit.Pending();

        var act = () => jobSeeker.ApproveByStaff(TestKit.Staff(), TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.StaffApprovalNotApplicable);
    }

    [Fact]
    public void Account_ApproveByStaff_WhenBannedOrActive_Throws()
    {
        var clock = TestKit.Clock();
        var banned = TestKit.Pending(ActorType.ExternalJobSite, clock);
        banned.Ban(TestKit.Admin(), "x", clock);
        var onBanned = () => banned.ApproveByStaff(TestKit.Staff(), clock);
        onBanned.ShouldBreakRule(AccountRuleCodes.Banned);

        var active = TestKit.Active(ActorType.ExternalJobSite, clock);
        var onActive = () => active.ApproveByStaff(TestKit.Staff(), clock);
        onActive.ShouldBreakRule(AccountRuleCodes.NotPending);
    }

    // -------------------------------------------------------------- approve by administrator

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public void Account_ApproveByAdministrator_FromPending_ActivatesAndRaisesBothEvents()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Pending(clock: clock);
        var admin = TestKit.Admin();

        account.ApproveByAdministrator(admin, clock);

        account.Standing.Should().Be(AccountStanding.Active);
        account.DomainEvents.Should().HaveCount(2);
        account.DomainEvents.First().Should().BeOfType<AccountActivatedDomainEvent>();
        var changed = account.DomainEvents.Last().Should().BeOfType<UserAccountStandingChangedDomainEvent>().Which;
        changed.From.Should().Be(AccountStanding.Pending);
        changed.To.Should().Be(AccountStanding.Active);
        changed.ActorId.Should().Be(admin.Id);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public void Account_ApproveByAdministrator_FromDeactivated_ReactivatesWithoutActivatedEvent()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        account.Deactivate(TestKit.Admin(), "pause", clock);
        account.ClearDomainEvents();

        account.ApproveByAdministrator(TestKit.Admin(), clock);

        account.Standing.Should().Be(AccountStanding.Active);
        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserAccountStandingChangedDomainEvent>()
            .Which.From.Should().Be(AccountStanding.Deactivated);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-03")]
    public void Account_ApproveByAdministrator_WhenBanned_ThrowsStateBanned()
    {
        var banned = TestKit.Banned();

        var act = () => banned.ApproveByAdministrator(TestKit.Admin(), TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.Banned, ErrorCodes.AdminStateBanned, BusinessRuleKind.Conflict);
        banned.Standing.Should().Be(AccountStanding.Banned);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-04")]
    public void Account_ApproveByAdministrator_ByNonAdministrator_ThrowsAdminOnly()
    {
        var account = TestKit.Pending();

        var act = () => account.ApproveByAdministrator(TestKit.Nobody(), TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.AdminOnly, ErrorCodes.AdminForbidden, BusinessRuleKind.Forbidden);
    }

    [Fact]
    public void Account_ApproveByAdministrator_WhenAlreadyActive_ThrowsAlreadyActive()
    {
        var active = TestKit.Active();

        var act = () => active.ApproveByAdministrator(TestKit.Admin(), TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.AlreadyActive, kind: BusinessRuleKind.Conflict);
    }

    // -------------------------------------------------------------- ban

    [Theory]
    [InlineData(AccountStanding.Pending)]
    [InlineData(AccountStanding.Active)]
    [InlineData(AccountStanding.Deactivated)]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public void Account_Ban_FromAnyNonBannedStanding_BansAndRecordsHistory(AccountStanding from)
    {
        var clock = TestKit.Clock();
        var account = from == AccountStanding.Pending ? TestKit.Pending(clock: clock) : TestKit.Active(clock: clock);
        if (from == AccountStanding.Deactivated)
        {
            account.Deactivate(TestKit.Admin(), "x", clock);
        }

        account.ClearDomainEvents();
        var admin = TestKit.Admin();

        account.Ban(admin, "policy violation", clock);

        account.Standing.Should().Be(AccountStanding.Banned);
        account.StatusHistory.Last().Should().Match<AccountStatusChange>(h => h.From == from && h.To == AccountStanding.Banned && h.Reason == "policy violation" && h.By == admin.Id);
        var changed = account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<UserAccountStandingChangedDomainEvent>().Which;
        changed.From.Should().Be(from);
        changed.To.Should().Be(AccountStanding.Banned);
    }

    [Fact]
    public void Account_Ban_DiscardsPendingActivationChallenge()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Pending(clock: clock);
        account.IssueActivationChallenge("h", clock);

        account.Ban(TestKit.Admin(), "x", clock);

        account.ActivationChallenge.Should().BeNull();
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-04")]
    public void Account_Ban_ByNonAdministrator_ThrowsAdminOnly()
    {
        var act = () => TestKit.Active().Ban(TestKit.Nobody(), "x", TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.AdminOnly, ErrorCodes.AdminForbidden);
    }

    [Fact]
    public void Account_Ban_WithoutReason_ThrowsReasonRequired()
    {
        var act = () => TestKit.Active().Ban(TestKit.Admin(), " ", TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.ReasonRequired, kind: BusinessRuleKind.InvalidInput);
    }

    [Fact]
    public void Account_Ban_WhenAlreadyBanned_ThrowsAlreadyBanned()
    {
        var act = () => TestKit.Banned().Ban(TestKit.Admin(), "again", TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.AlreadyBanned, ErrorCodes.AdminStateBanned);
    }

    // -------------------------------------------------------------- deactivate

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public void Account_Deactivate_ByAdministrator_RaisesSuspendedAndStandingChanged()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        var admin = TestKit.Admin();

        account.Deactivate(admin, "requested by ministry", clock);

        account.Standing.Should().Be(AccountStanding.Deactivated);
        account.DomainEvents.Should().HaveCount(2);
        var suspended = account.DomainEvents.OfType<AccountSuspendedDomainEvent>().Single();
        suspended.Reason.Should().Be("requested by ministry");
        suspended.Kind.Should().Be(SuspensionKind.Deactivated);
        suspended.ActorId.Should().Be(admin.Id);
        account.DomainEvents.OfType<UserAccountStandingChangedDomainEvent>().Single().To.Should().Be(AccountStanding.Deactivated);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-04")]
    public void Account_Deactivate_ByNonAdministrator_ThrowsAdminOnly()
    {
        var act = () => TestKit.Active().Deactivate(TestKit.Nobody(), "x", TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.AdminOnly, ErrorCodes.AdminForbidden);
    }

    [Fact]
    public void Account_Deactivate_InvalidStates_Throw()
    {
        var clock = TestKit.Clock();
        var pending = TestKit.Pending(clock: clock);
        var onPending = () => pending.Deactivate(TestKit.Admin(), "x", clock);
        onPending.ShouldBreakRule(AccountRuleCodes.NotActive);

        var onBanned = () => TestKit.Banned().Deactivate(TestKit.Admin(), "x", clock);
        onBanned.ShouldBreakRule(AccountRuleCodes.Banned);

        var deactivated = TestKit.Active(clock: clock);
        deactivated.Deactivate(TestKit.Admin(), "x", clock);
        var again = () => deactivated.Deactivate(TestKit.Admin(), "y", clock);
        again.ShouldBreakRule(AccountRuleCodes.AlreadyDeactivated);

        var noReason = () => TestKit.Active().Deactivate(TestKit.Admin(), "", clock);
        noReason.ShouldBreakRule(AccountRuleCodes.ReasonRequired);
    }

    [Theory]
    [InlineData(SuspensionKind.Deactivated)]
    [InlineData(SuspensionKind.DeletionRequested)]
    public void Account_RequestDeactivation_ByOwner_RaisesAccountSuspendedOnly(SuspensionKind kind)
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);

        account.RequestDeactivation(kind, "privacy setting", clock);

        account.Standing.Should().Be(AccountStanding.Deactivated);
        var suspended = account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountSuspendedDomainEvent>().Which;
        suspended.Kind.Should().Be(kind);
        suspended.ActorId.Should().Be(account.Id.Value);
    }

    [Fact]
    public void Account_RequestDeactivation_InvalidStates_Throw()
    {
        var pending = () => TestKit.Pending().RequestDeactivation(SuspensionKind.Deactivated, "x", TestKit.Clock());
        pending.ShouldBreakRule(AccountRuleCodes.NotActive);

        var noReason = () => TestKit.Active().RequestDeactivation(SuspensionKind.Deactivated, "", TestKit.Clock());
        noReason.ShouldBreakRule(AccountRuleCodes.ReasonRequired);
    }

    // -------------------------------------------------------------- reset credentials

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-02")]
    public void Account_ResetCredentials_ForcesPasswordChangeAndClearsLock()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        for (var i = 0; i < 5; i++)
        {
            account.AttemptLogin(false, clock);
        }

        account.IsLocked(TestKit.Start).Should().BeTrue();

        account.ResetCredentials(TestKit.Admin(), clock);

        account.MustChangePassword.Should().BeTrue();
        account.IsLocked(TestKit.Start).Should().BeFalse();
        account.FailedAttempts.Should().Be(0);
    }

    [Fact]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-04")]
    public void Account_ResetCredentials_ByNonAdministrator_ThrowsAdminOnly()
    {
        var act = () => TestKit.Active().ResetCredentials(TestKit.Nobody(), TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.AdminOnly, ErrorCodes.AdminForbidden);
    }

    // -------------------------------------------------------------- roles & event versions

    [Fact]
    public void Account_AssignRole_IsIdempotentAndRaisesEventOnlyOnChange()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        var role = RoleId.New();

        account.AssignRole(role, clock);
        account.AssignRole(role, clock);

        account.RoleAssignments.Count(r => r.RoleId == role).Should().Be(1);
        account.DomainEvents.OfType<AccountRolesChangedDomainEvent>().Should().ContainSingle();

        account.RemoveRole(role, clock);
        account.RemoveRole(role, clock);
        account.RoleAssignments.Any(r => r.RoleId == role).Should().BeFalse();
        account.DomainEvents.OfType<AccountRolesChangedDomainEvent>().Should().HaveCount(2);
    }

    [Fact]
    public void AggregateRoot_Version_IncrementsOncePerRaisedEvent()
    {
        var clock = TestKit.Clock();
        var account = Account.Register(TestKit.Details(), new PasswordHash("h"), clock);
        account.IssueActivationChallenge("h:1", clock);
        account.Activate("1", TestKit.PlainVerifier.Instance, clock);
        account.ApproveByAdministratorAfterDeactivation(clock);

        account.Version.Should().Be(account.DomainEvents.Count);
        account.ClearDomainEvents();
        account.Version.Should().BeGreaterThan(0);
    }
}

internal static class AccountTestExtensions
{
    /// <summary>Deactivate then re-approve: two more events on the same aggregate.</summary>
    public static void ApproveByAdministratorAfterDeactivation(this Account account, TimeProvider clock)
    {
        account.Deactivate(TestKit.Admin(), "pause", clock);
        account.ApproveByAdministrator(TestKit.Admin(), clock);
    }
}
