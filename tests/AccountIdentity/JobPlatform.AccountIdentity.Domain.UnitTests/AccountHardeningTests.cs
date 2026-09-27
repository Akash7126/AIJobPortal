using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;

namespace JobPlatform.AccountIdentity.Domain.UnitTests;

/// <summary>Credential hygiene: TOTP replay protection and transparent rehash of weaker password hashes.</summary>
public class AccountHardeningTests
{
    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public void Account_AttemptMfa_ByTimeStep_AcceptsAFreshStep_AndRecordsIt()
    {
        var clock = TestKit.Clock();
        var admin = TestKit.AdministratorAccount(clock);

        var result = admin.AttemptMfa((long?)100, clock);

        result.IsSuccess.Should().BeTrue();
        admin.MfaLastUsedTimeStep.Should().Be(100);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(99)]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public void Account_AttemptMfa_ByTimeStep_RefusesAReplayedOrOlderStep(long replayed)
    {
        var clock = TestKit.Clock();
        var admin = TestKit.AdministratorAccount(clock);
        admin.AttemptMfa((long?)100, clock).IsSuccess.Should().BeTrue();

        var result = admin.AttemptMfa((long?)replayed, clock);

        result.Outcome.Should().Be(LoginOutcome.InvalidCredentials, "a code that was already accepted is not a valid second factor");
        admin.MfaLastUsedTimeStep.Should().Be(100);
        admin.FailedAttempts.Should().Be(1, "a replay counts toward the lockout like any wrong code");
    }

    [Fact]
    public void Account_AttemptMfa_ByTimeStep_AcceptsALaterStep_AndRejectsNoMatch()
    {
        var clock = TestKit.Clock();
        var admin = TestKit.AdministratorAccount(clock);
        admin.AttemptMfa((long?)100, clock);

        admin.AttemptMfa((long?)101, clock).IsSuccess.Should().BeTrue();
        admin.AttemptMfa((long?)null, clock).Outcome.Should().Be(LoginOutcome.InvalidCredentials);
        admin.MfaLastUsedTimeStep.Should().Be(101, "a failed attempt never moves the recorded step");
    }

    [Fact]
    public void Account_UpgradePasswordHash_ReplacesTheHash_WithoutTouchingThePasswordMetadata()
    {
        var account = TestKit.Active();
        var changedAt = account.PasswordChangedAtUtc;
        var version = account.PasswordPolicyVersion;

        account.UpgradePasswordHash(new PasswordHash("h:stronger"));

        account.PasswordHash.Value.Should().Be("h:stronger");
        account.PasswordChangedAtUtc.Should().Be(changedAt);
        account.PasswordPolicyVersion.Should().Be(version);
        account.MustChangePassword.Should().BeFalse();
        account.DomainEvents.Should().BeEmpty("a rehash is not a business event");
    }
}
