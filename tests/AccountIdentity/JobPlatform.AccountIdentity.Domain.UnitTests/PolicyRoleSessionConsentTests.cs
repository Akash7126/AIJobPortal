using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.Consent;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.AccountIdentity.Domain.Sessions;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Security;

namespace JobPlatform.AccountIdentity.Domain.UnitTests;

public class PasswordPolicyTests
{
    [Fact]
    public void PasswordPolicy_CreateDefault_UsesAssumptionA02014()
    {
        var policy = TestKit.Policy();

        policy.MinLength.Should().Be(8);
        policy.RequireUpper.Should().BeTrue();
        policy.RequireLower.Should().BeTrue();
        policy.RequireDigit.Should().BeTrue();
        policy.PolicyVersion.Should().Be(1);
        policy.Id.Should().Be(PasswordPolicy.SingletonId);
    }

    [Theory]
    [InlineData("Passw0rdOK", 0)]
    [InlineData("Aa1bcdef", 0)]
    [InlineData("Aa1bcde", 1)]
    [InlineData("passw0rdok", 1)]
    [InlineData("PASSW0RDOK", 1)]
    [InlineData("PasswordOK", 1)]
    [InlineData("", 4)]
    [InlineData(null, 4)]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-01")]
    public void PasswordPolicy_Validate_ReportsEachViolatedRule(string? password, int violations) =>
        TestKit.Policy().Validate(password).Should().HaveCount(violations);

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-01")]
    public void PasswordPolicy_EnsureCompliant_AcceptsExactlyTheMinimumPolicy() => TestKit.Policy().EnsureCompliant("Abcdef1x");

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-02")]
    public void PasswordPolicy_EnsureCompliant_WeakPassword_ThrowsInvalidField()
    {
        var act = () => TestKit.Policy().EnsureCompliant("weak");

        act.ShouldBreakRule(PasswordPolicyRuleCodes.WeakPassword, ErrorCodes.AuthInvalidField, BusinessRuleKind.InvalidInput);
    }

    [Fact]
    public void PasswordPolicy_Validate_RejectsPasswordsOverTheMaximumLength() =>
        TestKit.Policy().Validate("Aa1" + new string('x', 130)).Should().Contain(PasswordViolations.MaxLength);

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-03")]
    public void PasswordPolicy_Configure_IncrementsVersionAndRaisesEvent()
    {
        var clock = TestKit.Clock();
        var policy = TestKit.Policy(clock);
        var admin = TestKit.Admin();
        clock.Advance(TimeSpan.FromHours(1));

        policy.Configure(admin, 12, true, false, true, clock);

        policy.MinLength.Should().Be(12);
        policy.RequireLower.Should().BeFalse();
        policy.PolicyVersion.Should().Be(2);
        policy.UpdatedBy.Should().Be(admin.Id);
        policy.UpdatedAtUtc.Should().Be(TestKit.Start.AddHours(1));
        policy.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PasswordPolicyConfiguredDomainEvent>().Which.PolicyVersion.Should().Be(2);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-03")]
    public void PasswordPolicy_Configure_ByNonAdministrator_ThrowsAdminOnly()
    {
        var act = () => TestKit.Policy().Configure(TestKit.Nobody(), 10, true, true, true, TestKit.Clock());

        act.ShouldBreakRule(PasswordPolicyRuleCodes.AdminOnly, ErrorCodes.AuthForbidden, BusinessRuleKind.Forbidden);
    }

    [Theory]
    [InlineData(7, true, true, true)]
    [InlineData(129, true, true, true)]
    [InlineData(8, false, false, false)]
    public void PasswordPolicy_Configure_InvalidParameters_Throw(int min, bool upper, bool lower, bool digit)
    {
        var act = () => TestKit.Policy().Configure(TestKit.Admin(), min, upper, lower, digit, TestKit.Clock());

        act.ShouldBreakRule(PasswordPolicyRuleCodes.InvalidParameters);
    }
}

public class RoleAndAccessPolicyTests
{
    private static Role NewRole(params string[] permissions) => Role.Create(RoleId.New(), "Custom", false, permissions);

    [Fact]
    public void Role_Create_HoldsTheGivenPermissions()
    {
        var role = NewRole(Permissions.JobsBrowse, Permissions.ProfileManage);

        role.Has(Permissions.JobsBrowse).Should().BeTrue();
        role.Has(Permissions.AccountsBan).Should().BeFalse();
        role.ToRolePermissions().Permissions.Should().BeEquivalentTo(Permissions.JobsBrowse, Permissions.ProfileManage);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-04")]
    public void Role_GrantAndRevoke_ChangeThePermissionSetAndRaiseEvents()
    {
        var clock = TestKit.Clock();
        var role = NewRole(Permissions.JobsBrowse);

        role.GrantPermission(TestKit.Admin(), Permissions.AccountsRead, clock);
        role.Has(Permissions.AccountsRead).Should().BeTrue();
        role.RevokePermission(TestKit.Admin(), Permissions.JobsBrowse, clock);
        role.Has(Permissions.JobsBrowse).Should().BeFalse();

        role.DomainEvents.OfType<RolePermissionsChangedDomainEvent>().Should().HaveCount(2);
    }

    [Fact]
    public void Role_Grant_IsIdempotent_Revoke_OfMissingPermissionIsNoOp()
    {
        var clock = TestKit.Clock();
        var role = NewRole(Permissions.JobsBrowse);

        role.GrantPermission(TestKit.Admin(), Permissions.JobsBrowse, clock);
        role.RevokePermission(TestKit.Admin(), Permissions.AccountsBan, clock);

        role.DomainEvents.Should().BeEmpty();
        role.Permissions.Should().ContainSingle();
    }

    [Fact]
    public void Role_UnknownPermission_IsRejected()
    {
        var grant = () => NewRole().GrantPermission(TestKit.Admin(), "made.up", TestKit.Clock());
        grant.ShouldBreakRule(RoleRuleCodes.UnknownPermission, kind: BusinessRuleKind.InvalidInput);
        var create = () => NewRole("made.up");
        create.ShouldBreakRule(RoleRuleCodes.UnknownPermission);
        var revoke = () => NewRole().RevokePermission(TestKit.Admin(), "made.up", TestKit.Clock());
        revoke.ShouldBreakRule(RoleRuleCodes.UnknownPermission);
        var grantExisting = () => NewRole(Permissions.JobsBrowse).GrantPermission(TestKit.Admin(), "made.up", TestKit.Clock());
        grantExisting.ShouldBreakRule(RoleRuleCodes.UnknownPermission);
    }

    [Fact]
    public void Role_Changes_RequireAnAdministrator()
    {
        var role = NewRole(Permissions.JobsBrowse);
        var grant = () => role.GrantPermission(TestKit.Nobody(), Permissions.AccountsRead, TestKit.Clock());
        grant.ShouldBreakRule(RoleRuleCodes.AdminOnly, ErrorCodes.AuthForbidden);
        var revoke = () => role.RevokePermission(TestKit.Nobody(), Permissions.JobsBrowse, TestKit.Clock());
        revoke.ShouldBreakRule(RoleRuleCodes.AdminOnly);
    }

    [Fact]
    public void Role_Administrator_CannotLoseTheRolesManagePermission()
    {
        var admin = Role.Create(WellKnownRoles.Administrator, "Administrator", true, Permissions.All);

        var act = () => admin.RevokePermission(TestKit.Admin(), Permissions.RolesManage, TestKit.Clock());

        act.ShouldBreakRule(RoleRuleCodes.LastAdminPermission);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-01")]
    public void AccessPolicy_Authorise_AllowsWhenAnyRoleHoldsThePermission()
    {
        var roles = new[] { NewRole(Permissions.JobsBrowse).ToRolePermissions(), NewRole(Permissions.AccountsRead).ToRolePermissions() };

        AccessPolicy.Authorise(roles, Permissions.AccountsRead).Allowed.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-03")]
    [Trait("AC", "AC-02")]
    public void AccessPolicy_Authorise_DeniesWithForbiddenCodeWhenNoRoleHoldsIt()
    {
        var decision = AccessPolicy.Authorise(new[] { NewRole(Permissions.JobsBrowse).ToRolePermissions() }, Permissions.AccountsBan);

        decision.Allowed.Should().BeFalse();
        decision.Code.Should().Be(ErrorCodes.AuthForbidden);
        decision.Reason.Should().Contain(Permissions.AccountsBan);
    }

    [Fact]
    public void AccessPolicy_Authorise_WithNoRoles_Denies() =>
        AccessPolicy.Authorise(Array.Empty<RolePermissions>(), Permissions.JobsBrowse).Allowed.Should().BeFalse();

    [Fact]
    public void WellKnownRoles_SeedDefinitions_GiveEachActorItsOwnRole()
    {
        WellKnownRoles.ForActor(ActorType.JobSeeker).Should().Be(WellKnownRoles.JobSeeker);
        WellKnownRoles.ForActor(ActorType.Employer).Should().Be(WellKnownRoles.Employer);
        WellKnownRoles.ForActor(ActorType.Administrator).Should().Be(WellKnownRoles.Administrator);
        WellKnownRoles.ForActor(ActorType.ExternalJobSite).Should().Be(WellKnownRoles.ExternalJobSite);
        WellKnownRoles.ForActor(ActorType.Guest).Should().Be(WellKnownRoles.Guest);
        WellKnownRoles.Definitions.Should().HaveCount(5);
        WellKnownRoles.Definitions.Single(d => d.Id == WellKnownRoles.Administrator).Permissions.Should().BeEquivalentTo(Permissions.All);
        WellKnownRoles.Definitions.Single(d => d.Id == WellKnownRoles.JobSeeker).Permissions.Should().NotContain(Permissions.AccountsBan);
    }
}

public class UserSessionTests
{
    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-01")]
    public void UserSession_IsExpired_WhenIdlePeriodReachesThirtyMinutes()
    {
        var clock = TestKit.Clock();
        var session = UserSession.Create(Guid.NewGuid(), 30, "h", clock);

        clock.Advance(TimeSpan.FromMinutes(29) + TimeSpan.FromSeconds(59));
        session.IsExpired(clock).Should().BeFalse();

        clock.Advance(TimeSpan.FromSeconds(1));
        session.IsExpired(clock).Should().BeTrue();
        session.IsUsable(clock).Should().BeFalse();
        var touch = () => session.Touch(clock);
        touch.ShouldBreakRule(SessionRuleCodes.Expired, ErrorCodes.AuthSessionExpired, BusinessRuleKind.Unauthorized);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-02")]
    public void UserSession_Touch_ResetsTheIdleTimer()
    {
        var clock = TestKit.Clock();
        var session = UserSession.Create(Guid.NewGuid(), 30, "h", clock);
        clock.Advance(TimeSpan.FromMinutes(20));

        session.Touch(clock);
        clock.Advance(TimeSpan.FromMinutes(20));

        session.IsExpired(clock).Should().BeFalse("activity at minute 20 restarts the 30 minute window");
        session.LastActivityUtc.Should().Be(TestKit.Start.AddMinutes(20));
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-03")]
    public void UserSession_CapturesTimeoutAtCreation_LaterSettingChangesDoNotAffectIt()
    {
        var clock = TestKit.Clock();
        var setting = SessionTimeoutSetting.CreateDefault(clock);
        var existing = UserSession.Create(Guid.NewGuid(), setting.IdleTimeoutMinutes, "h", clock);

        setting.Configure(TestKit.Admin(), 5, clock);
        var created = UserSession.Create(Guid.NewGuid(), setting.IdleTimeoutMinutes, "h", clock);
        clock.Advance(TimeSpan.FromMinutes(10));

        existing.IdleTimeoutMinutes.Should().Be(30);
        existing.IsExpired(clock).Should().BeFalse();
        created.IdleTimeoutMinutes.Should().Be(5);
        created.IsExpired(clock).Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-04")]
    [Trait("AC", "AC-04")]
    public void UserSession_Invalidate_MakesTheSessionUnusableImmediately()
    {
        var clock = TestKit.Clock();
        var session = UserSession.Create(Guid.NewGuid(), 30, "h", clock);

        session.Invalidate();

        session.Status.Should().Be(SessionStatus.Invalidated);
        session.IsUsable(clock).Should().BeFalse();
        var act = () => session.EnsureUsable(clock);
        act.ShouldBreakRule(SessionRuleCodes.Invalidated, ErrorCodes.AuthSessionExpired);
    }

    [Fact]
    public void UserSession_SnapshotRoundTrip_PreservesState_AndRotatesRefreshToken()
    {
        var clock = TestKit.Clock();
        var session = UserSession.Create(Guid.NewGuid(), 45, "hash-1", clock);
        session.RotateRefreshToken("hash-2");

        var restored = UserSession.Rehydrate(session.ToSnapshot());

        restored.SessionId.Should().Be(session.SessionId);
        restored.AccountId.Should().Be(session.AccountId);
        restored.IdleTimeoutMinutes.Should().Be(45);
        restored.RefreshTokenHash.Should().Be("hash-2");
        restored.IdleTimeout.Should().Be(TimeSpan.FromMinutes(45));
    }

    [Fact]
    public void SessionTimeoutSetting_Default_IsThirtyMinutes_AndIsVersioned()
    {
        var clock = TestKit.Clock();
        var setting = SessionTimeoutSetting.CreateDefault(clock);
        var admin = TestKit.Admin();

        setting.IdleTimeoutMinutes.Should().Be(30);
        setting.Configure(admin, 60, clock);

        setting.IdleTimeoutMinutes.Should().Be(60);
        setting.SettingVersion.Should().Be(2);
        setting.UpdatedBy.Should().Be(admin.Id);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(481)]
    public void SessionTimeoutSetting_Configure_OutsideRange_Throws(int minutes)
    {
        var act = () => SessionTimeoutSetting.CreateDefault(TestKit.Clock()).Configure(TestKit.Admin(), minutes, TestKit.Clock());

        act.ShouldBreakRule(SessionRuleCodes.InvalidTimeout, kind: BusinessRuleKind.InvalidInput);
    }

    [Fact]
    public void SessionTimeoutSetting_Configure_ByNonAdministrator_Throws()
    {
        var act = () => SessionTimeoutSetting.CreateDefault(TestKit.Clock()).Configure(TestKit.Nobody(), 30, TestKit.Clock());

        act.ShouldBreakRule(SessionRuleCodes.AdminOnly, ErrorCodes.AuthForbidden);
    }
}

public class PrivacyConsentTests
{
    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-04")]
    public void ConsentChoices_NecessaryOnly_WithholdsEverythingNonEssential()
    {
        var choices = ConsentChoices.NecessaryOnly;

        choices.Necessary.Should().BeTrue();
        choices.Analytics.Should().BeFalse();
        choices.Preferences.Should().BeFalse();
        choices.Marketing.Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-01")]
    public void PrivacyConsent_Create_StoresTheGuestDecisionForThePolicyVersion()
    {
        var clock = TestKit.Clock();
        var guest = Guid.NewGuid();

        var consent = PrivacyConsent.Create(guest, "2026-01", new ConsentChoices(true, false, true), Language.Ar, clock);

        consent.GuestId.Should().Be(guest);
        consent.PolicyVersion.Should().Be("2026-01");
        consent.Choices.Analytics.Should().BeTrue();
        consent.Choices.Marketing.Should().BeTrue();
        consent.Locale.Should().Be(Language.Ar);
        consent.DecidedAtUtc.Should().Be(TestKit.Start);
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-05")]
    public void PrivacyConsent_Record_WithSameChoices_IsIdempotent()
    {
        var clock = TestKit.Clock();
        var consent = PrivacyConsent.Create(Guid.NewGuid(), "v1", new ConsentChoices(true, true, false), Language.En, clock);
        clock.Advance(TimeSpan.FromDays(1));

        var changed = consent.Record(new ConsentChoices(true, true, false), Language.En, clock);

        changed.Should().BeFalse();
        consent.DecidedAtUtc.Should().Be(TestKit.Start, "an identical decision does not rewrite the record");
    }

    [Fact]
    [Trait("Story", "US-4.1-04")]
    [Trait("AC", "AC-05")]
    public void PrivacyConsent_Record_WithNewChoices_UpdatesTheSameRecord()
    {
        var clock = TestKit.Clock();
        var consent = PrivacyConsent.Create(Guid.NewGuid(), "v1", ConsentChoices.NecessaryOnly, Language.En, clock);
        var id = consent.Id;
        clock.Advance(TimeSpan.FromDays(1));

        var changed = consent.Record(new ConsentChoices(true, false, false), Language.Ar, clock);

        changed.Should().BeTrue();
        consent.Id.Should().Be(id);
        consent.Choices.Analytics.Should().BeTrue();
        consent.Locale.Should().Be(Language.Ar);
        consent.DecidedAtUtc.Should().Be(TestKit.Start.AddDays(1));
    }

    [Fact]
    public void PrivacyConsent_Create_WithoutPolicyVersion_Throws()
    {
        var act = () => PrivacyConsent.Create(Guid.NewGuid(), " ", ConsentChoices.NecessaryOnly, Language.En, TestKit.Clock());

        act.ShouldBreakRule(ConsentRuleCodes.PolicyVersionRequired);
    }
}
