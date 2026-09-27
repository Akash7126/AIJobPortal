using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AccountIdentity.Domain.UnitTests;

public class AccountAuthenticationTests
{
    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-01")]
    public void Account_AttemptLogin_WithCorrectPasswordOnActiveAccount_Succeeds()
    {
        var account = TestKit.Active();

        var result = account.AttemptLogin(true, TestKit.Clock());

        result.IsSuccess.Should().BeTrue();
        result.Outcome.Should().Be(LoginOutcome.Succeeded);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-03")]
    public void Account_AttemptLogin_FifthConsecutiveFailure_LocksForFifteenMinutes()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);

        for (var i = 1; i <= 4; i++)
        {
            account.AttemptLogin(false, clock).Outcome.Should().Be(LoginOutcome.InvalidCredentials, $"attempt {i} is still below the cap");
        }

        var fifth = account.AttemptLogin(false, clock);

        fifth.Outcome.Should().Be(LoginOutcome.Locked);
        fifth.RuleCode.Should().Be(AccountRuleCodes.Locked);
        fifth.ExternalCode.Should().Be(ErrorCodes.AuthRateLimited);
        fifth.LockedUntilUtc.Should().Be(TestKit.Start.AddMinutes(15));
        account.IsLocked(TestKit.Start.AddMinutes(14)).Should().BeTrue();
        account.IsLocked(TestKit.Start.AddMinutes(15)).Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-03")]
    public void Account_AttemptLogin_WhileLocked_RefusesEvenWithCorrectPasswordAndDoesNotExtendTheLock()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        for (var i = 0; i < 5; i++)
        {
            account.AttemptLogin(false, clock);
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        var result = account.AttemptLogin(true, clock);

        result.Outcome.Should().Be(LoginOutcome.Locked);
        result.LockedUntilUtc.Should().Be(TestKit.Start.AddMinutes(15));
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-03")]
    public void Account_AttemptLogin_AfterLockExpires_AllowsLoginAgain()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        for (var i = 0; i < 5; i++)
        {
            account.AttemptLogin(false, clock);
        }

        clock.Advance(TimeSpan.FromMinutes(15));

        account.AttemptLogin(true, clock).IsSuccess.Should().BeTrue();
        account.LockedUntilUtc.Should().BeNull();
        account.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public void Account_AttemptLogin_FailuresOlderThanTheWindow_DoNotAccumulate()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        for (var i = 0; i < 4; i++)
        {
            account.AttemptLogin(false, clock);
        }

        clock.Advance(TimeSpan.FromMinutes(16));
        var result = account.AttemptLogin(false, clock);

        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        account.FailedAttempts.Should().Be(1);
    }

    [Fact]
    public void Account_AttemptLogin_SuccessResetsTheFailureCounter()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        for (var i = 0; i < 4; i++)
        {
            account.AttemptLogin(false, clock);
        }

        account.AttemptLogin(true, clock);
        account.AttemptLogin(false, clock).Outcome.Should().Be(LoginOutcome.InvalidCredentials);

        account.FailedAttempts.Should().Be(1);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-04")]
    public void Account_AttemptLogin_FailureResultNeverRevealsWhichCredentialWasWrong()
    {
        var clock = TestKit.Clock();
        var wrongPassword = TestKit.Active(clock: clock).AttemptLogin(false, clock);

        wrongPassword.Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        wrongPassword.ExternalCode.Should().Be(ErrorCodes.AuthInvalidCredentials);
        wrongPassword.RuleCode.Should().BeNull();
        wrongPassword.LockedUntilUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(AccountStanding.Pending, ErrorCodes.AuthAccountPending)]
    [InlineData(AccountStanding.Banned, ErrorCodes.AdminStateBanned)]
    [InlineData(AccountStanding.Deactivated, ErrorCodes.AuthAccountDeactivated)]
    [Trait("Story", "US-3.1.4-03")]
    [Trait("AC", "AC-01")]
    public void Account_AttemptLogin_NonActiveStanding_TakesEffectOnNextAuthentication(AccountStanding standing, string external)
    {
        var clock = TestKit.Clock();
        var account = standing switch
        {
            AccountStanding.Pending => TestKit.Pending(clock: clock),
            AccountStanding.Banned => TestKit.Banned(clock),
            _ => TestKit.Active(clock: clock)
        };
        if (standing == AccountStanding.Deactivated)
        {
            account.Deactivate(TestKit.Admin(), "x", clock);
        }

        var result = account.AttemptLogin(true, clock);

        result.Outcome.Should().Be(LoginOutcome.NotActive);
        result.ExternalCode.Should().Be(external);
    }

    [Fact]
    public void Account_AttemptLogin_WhenSecondFactorPending_KeepsTheFailureCounter()
    {
        var clock = TestKit.Clock();
        var admin = TestKit.AdministratorAccount(clock);
        admin.AttemptLogin(false, clock);
        admin.AttemptLogin(false, clock);

        admin.AttemptLogin(true, clock).IsSuccess.Should().BeTrue();

        admin.FailedAttempts.Should().Be(2, "a stolen password must not reset the MFA brute-force counter");
    }

    // -------------------------------------------------------------- MFA

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public void Account_MfaEnrolment_BeginThenConfirm_EnablesMfa()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        account.MfaRequired.Should().BeFalse();

        account.BeginMfaEnrollment("protected-seed");
        account.MfaEnabled.Should().BeFalse("enrolment is not complete until a code is verified");
        account.ConfirmMfaEnrollment(clock);

        account.MfaEnabled.Should().BeTrue();
        account.MfaRequired.Should().BeTrue();
        account.MfaEnrolledAtUtc.Should().Be(TestKit.Start);
        account.MfaSecret.Should().Be("protected-seed");
    }

    [Fact]
    public void Account_MfaEnrolment_InvalidTransitions_Throw()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        var confirmFirst = () => account.ConfirmMfaEnrollment(clock);
        confirmFirst.ShouldBreakRule(AccountRuleCodes.MfaNotStarted);

        account.BeginMfaEnrollment("s");
        account.ConfirmMfaEnrollment(clock);
        var again = () => account.BeginMfaEnrollment("t");
        again.ShouldBreakRule(AccountRuleCodes.MfaAlreadyEnrolled);
        account.ConfirmMfaEnrollment(clock);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public void Account_EnsureMfaSatisfied_ForAdministrator_RequiresVerification()
    {
        var admin = TestKit.AdministratorAccount();

        var withoutMfa = () => admin.EnsureMfaSatisfied(false);
        withoutMfa.ShouldBreakRule(AccountRuleCodes.MfaRequiredForAdmin, ErrorCodes.AuthMfaRequired);
        admin.EnsureMfaSatisfied(true);
    }

    [Fact]
    public void Account_EnsureMfaSatisfied_ForOrdinaryUserWithoutMfa_IsNotRequired() => TestKit.Active().EnsureMfaSatisfied(false);

    [Fact]
    public void Account_AttemptMfa_WrongCodesCountTowardLockout_AndSuccessClearsThem()
    {
        var clock = TestKit.Clock();
        var admin = TestKit.AdministratorAccount(clock);
        for (var i = 0; i < 4; i++)
        {
            admin.AttemptMfa(false, clock).Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        }

        admin.AttemptMfa(false, clock).Outcome.Should().Be(LoginOutcome.Locked);
        admin.AttemptMfa(true, clock).Outcome.Should().Be(LoginOutcome.Locked);

        clock.Advance(TimeSpan.FromMinutes(15));
        admin.AttemptMfa(true, clock).IsSuccess.Should().BeTrue();
        admin.FailedAttempts.Should().Be(0);
    }

    [Fact]
    public void Account_AttemptMfa_OnInactiveAccount_IsRefused()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        account.Ban(TestKit.Admin(), "x", clock);

        account.AttemptMfa(true, clock).Outcome.Should().Be(LoginOutcome.NotActive);
    }

    [Fact]
    public void Account_CanAuthenticate_RequiresActiveAndNotLocked()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        account.CanAuthenticate(TestKit.Start).Should().BeTrue();
        for (var i = 0; i < 5; i++)
        {
            account.AttemptLogin(false, clock);
        }

        account.CanAuthenticate(TestKit.Start).Should().BeFalse();
        TestKit.Pending().CanAuthenticate(TestKit.Start).Should().BeFalse();
    }

    // -------------------------------------------------------------- change password (INV-07)

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-01")]
    public void Account_ChangePassword_CompliantPassword_IsAccepted()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        clock.Advance(TimeSpan.FromDays(1));

        account.ChangePassword("NewPassw0rd", new PasswordHash("h:new"), TestKit.Policy(clock), clock);

        account.PasswordHash.Value.Should().Be("h:new");
        account.PasswordChangedAtUtc.Should().Be(TestKit.Start.AddDays(1));
        account.MustChangePassword.Should().BeFalse();
    }

    [Theory]
    [InlineData("Sh0rt", new[] { PasswordViolations.MinLength })]
    [InlineData("alllowercase1", new[] { PasswordViolations.Uppercase })]
    [InlineData("ALLUPPERCASE1", new[] { PasswordViolations.Lowercase })]
    [InlineData("NoDigitsHere", new[] { PasswordViolations.Digit })]
    [InlineData("short", new[] { PasswordViolations.MinLength, PasswordViolations.Uppercase, PasswordViolations.Digit })]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-02")]
    public void Account_ChangePassword_WeakPassword_ThrowsInvalidFieldWithViolations(string password, string[] expected)
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);

        var act = () => account.ChangePassword(password, new PasswordHash("h"), TestKit.Policy(clock), clock);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(PasswordPolicyRuleCodes.WeakPassword);
        ex.ExternalCode.Should().Be(ErrorCodes.AuthInvalidField);
        ex.Kind.Should().Be(BusinessRuleKind.InvalidInput);
        ((string[])ex.Args["violations"]!).Should().BeEquivalentTo(expected);
        ex.Message.Should().NotContain(password);
    }

    [Fact]
    public void Account_ChangePassword_ClearsMustChangeFlagAfterReset()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        account.ResetCredentials(TestKit.Admin(), clock);

        account.ChangePassword("NewPassw0rd", new PasswordHash("h"), TestKit.Policy(clock), clock);

        account.MustChangePassword.Should().BeFalse();
    }

    [Fact]
    public void Account_ChangePassword_WhenNotActive_Throws()
    {
        var pending = TestKit.Pending();

        var act = () => pending.ChangePassword("NewPassw0rd", new PasswordHash("h"), TestKit.Policy(), TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.NotActive);
    }

    [Fact]
    [Trait("Story", "US-3.1.5-02")]
    [Trait("AC", "AC-03")]
    public void Account_PasswordPolicyChange_AppliesOnlyAtTheNextPasswordChange()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Active(clock: clock);
        var policy = TestKit.Policy(clock);
        var admin = TestKit.Admin();

        policy.Configure(admin, 12, true, true, true, clock);

        account.IsBehindPasswordPolicy(policy).Should().BeTrue("existing users are flagged, not forced");
        account.AttemptLogin(true, clock).IsSuccess.Should().BeTrue("their current password keeps working");
        var tooShortForNewPolicy = () => account.ChangePassword("Abcdef1x", new PasswordHash("h"), policy, clock);
        tooShortForNewPolicy.ShouldBreakRule(PasswordPolicyRuleCodes.WeakPassword);

        account.ChangePassword("Abcdefgh1234", new PasswordHash("h"), policy, clock);
        account.PasswordPolicyVersion.Should().Be(policy.PolicyVersion);
        account.IsBehindPasswordPolicy(policy).Should().BeFalse();
    }
}
