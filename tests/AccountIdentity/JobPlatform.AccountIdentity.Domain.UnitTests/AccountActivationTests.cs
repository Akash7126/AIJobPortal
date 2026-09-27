using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using Microsoft.Extensions.Time.Testing;

namespace JobPlatform.AccountIdentity.Domain.UnitTests;

public class AccountActivationTests
{
    private static readonly TestKit.PlainVerifier Verifier = TestKit.PlainVerifier.Instance;

    private static (Account Account, FakeTimeProvider Clock) PendingWithCode(ActorType type = ActorType.JobSeeker, string code = "123456")
    {
        var clock = TestKit.Clock();
        var account = TestKit.Pending(type, clock);
        account.IssueActivationChallenge(TestKit.PlainVerifier.Hash(code), clock);
        return (account, clock);
    }

    [Fact]
    public void Account_IssueActivationChallenge_SetsTenMinuteExpiryAndStoresOnlyTheHash()
    {
        var (account, _) = PendingWithCode();

        account.ActivationChallenge!.ExpiresAtUtc.Should().Be(TestKit.Start.AddMinutes(10));
        account.ActivationChallenge.IssuedAtUtc.Should().Be(TestKit.Start);
        account.ActivationChallenge.CodeHash.Should().Be(TestKit.PlainVerifier.Hash("123456"), "the aggregate keeps the hash the application computed, never the code");
        account.ActivationChallenge.Attempts.Should().Be(0);
    }

    [Fact]
    public void Account_IssueActivationChallenge_Reissue_ReplacesCodeAndResetsAttempts()
    {
        var (account, clock) = PendingWithCode();
        var act = () => account.Activate("000000", Verifier, clock);
        act.ShouldBreakRule(AccountRuleCodes.CodeInvalidOrExpired);
        account.ActivationChallenge!.Attempts.Should().Be(1);

        clock.Advance(TimeSpan.FromMinutes(3));
        account.IssueActivationChallenge(TestKit.PlainVerifier.Hash("654321"), clock);

        account.ActivationChallenge.CodeHash.Should().Be("h:654321");
        account.ActivationChallenge.Attempts.Should().Be(0);
        account.ActivationChallenge.ExpiresAtUtc.Should().Be(TestKit.Start.AddMinutes(13));
    }

    [Fact]
    public void Account_IssueActivationChallenge_ForPartner_ThrowsOtpNotApplicable()
    {
        var partner = TestKit.Pending(ActorType.ExternalJobSite);

        var act = () => partner.IssueActivationChallenge("h", TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.OtpNotApplicable);
    }

    [Fact]
    public void Account_IssueActivationChallenge_WhenActive_ThrowsNotPending()
    {
        var active = TestKit.Active();

        var act = () => active.IssueActivationChallenge("h", TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.NotPending, kind: BusinessRuleKind.Conflict);
    }

    [Theory]
    [InlineData(ActorType.JobSeeker)]
    [InlineData(ActorType.Employer)]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-01")]
    public void Account_Activate_WithCorrectCode_MovesPendingToActive(ActorType type)
    {
        var (account, clock) = PendingWithCode(type);
        clock.Advance(TimeSpan.FromMinutes(2));

        account.Activate("123456", Verifier, clock);

        account.Standing.Should().Be(AccountStanding.Active);
        account.ActivatedAtUtc.Should().Be(TestKit.Start.AddMinutes(2));
        account.ActivationChallenge.Should().BeNull();
        account.StatusHistory.Last().Should().Match<AccountStatusChange>(h => h.From == AccountStanding.Pending && h.To == AccountStanding.Active);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-02")]
    [Trait("AC", "AC-01")]
    public void Account_Activate_Employer_WithCorrectOtp_Activates()
    {
        var (account, clock) = PendingWithCode(ActorType.Employer);

        account.Activate("123456", Verifier, clock);

        account.Standing.Should().Be(AccountStanding.Active);
    }

    [Theory]
    [InlineData(ActorType.JobSeeker, ErrorCodes.JobSeekerExpired)]
    [InlineData(ActorType.Employer, ErrorCodes.EmployerExpired)]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-02")]
    public void Account_Activate_WithWrongCode_ThrowsActorSpecificExpiredCode(ActorType type, string external)
    {
        var (account, clock) = PendingWithCode(type);

        var act = () => account.Activate("999999", Verifier, clock);

        act.ShouldBreakRule(AccountRuleCodes.CodeInvalidOrExpired, external);
        account.Standing.Should().Be(AccountStanding.Pending);
        account.ActivationChallenge!.Attempts.Should().Be(1, "a failed attempt must be recorded so the cap can be enforced");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-02")]
    [Trait("AC", "AC-02")]
    public void Account_Activate_Employer_WithWrongOtp_ThrowsEmployerExpired()
    {
        var (account, clock) = PendingWithCode(ActorType.Employer);

        var act = () => account.Activate("000000", Verifier, clock);

        act.ShouldBreakRule(AccountRuleCodes.CodeInvalidOrExpired, ErrorCodes.EmployerExpired);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-02")]
    public void Account_Activate_ExactlyAtTenMinutes_StillAccepted()
    {
        var (account, clock) = PendingWithCode();
        clock.Advance(TimeSpan.FromMinutes(10));

        account.Activate("123456", Verifier, clock);

        account.Standing.Should().Be(AccountStanding.Active);
    }

    [Fact]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-02")]
    public void Account_Activate_OneSecondAfterTenMinutes_ThrowsExpired()
    {
        var (account, clock) = PendingWithCode();
        clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        var act = () => account.Activate("123456", Verifier, clock);

        act.ShouldBreakRule(AccountRuleCodes.CodeInvalidOrExpired, ErrorCodes.JobSeekerExpired);
        account.Standing.Should().Be(AccountStanding.Pending);
    }

    [Theory]
    [InlineData(ActorType.JobSeeker, ErrorCodes.JobSeekerRateLimited)]
    [InlineData(ActorType.Employer, ErrorCodes.EmployerRateLimited)]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-03")]
    public void Account_Activate_SixthAttempt_IsBlockedEvenWithCorrectCode(ActorType type, string external)
    {
        var (account, clock) = PendingWithCode(type);
        for (var i = 0; i < 5; i++)
        {
            var wrong = () => account.Activate("000000", Verifier, clock);
            wrong.ShouldBreakRule(AccountRuleCodes.CodeInvalidOrExpired);
        }

        var act = () => account.Activate("123456", Verifier, clock);

        act.ShouldBreakRule(AccountRuleCodes.TooManyAttempts, external, BusinessRuleKind.RateLimited);
        account.Standing.Should().Be(AccountStanding.Pending);
        account.ActivationChallenge!.Attempts.Should().Be(5, "the blocked attempt must not be counted again");
    }

    [Fact]
    [Trait("Story", "US-3.1.2-02")]
    [Trait("AC", "AC-03")]
    public void Account_Activate_Employer_SixthOtpAttempt_ThrowsEmployerRateLimited()
    {
        var (account, clock) = PendingWithCode(ActorType.Employer);
        for (var i = 0; i < 5; i++)
        {
            var wrong = () => account.Activate("000000", Verifier, clock);
            wrong.ShouldBreakRule(AccountRuleCodes.CodeInvalidOrExpired);
        }

        var act = () => account.Activate("123456", Verifier, clock);

        act.ShouldBreakRule(AccountRuleCodes.TooManyAttempts, ErrorCodes.EmployerRateLimited);
    }

    [Fact]
    public void Account_Activate_WithoutChallenge_ThrowsInvalidOrExpired()
    {
        var account = TestKit.Pending();

        var act = () => account.Activate("123456", Verifier, TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.CodeInvalidOrExpired);
    }

    [Fact]
    public void Account_Activate_WhenAlreadyActive_ThrowsNotPending()
    {
        var active = TestKit.Active();

        var act = () => active.Activate("123456", Verifier, TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.NotPending);
    }

    [Fact]
    public void Account_Activate_WhenBanned_ThrowsBannedNotApprovable()
    {
        var (account, clock) = PendingWithCode();
        account.Ban(TestKit.Admin(), "fraud", clock);

        var act = () => account.Activate("123456", Verifier, clock);

        act.ShouldBreakRule(AccountRuleCodes.Banned, ErrorCodes.AdminStateBanned);
    }

    [Theory]
    [InlineData(ActorType.JobSeeker)]
    [InlineData(ActorType.Employer)]
    [Trait("Story", "US-3.1.1-02")]
    [Trait("AC", "AC-04")]
    public void Account_Activate_RaisesAccountActivatedWithActorType(ActorType type)
    {
        var (account, clock) = PendingWithCode(type);
        account.ClearDomainEvents();

        account.Activate("123456", Verifier, clock);

        var raised = account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountActivatedDomainEvent>().Which;
        raised.ActorType.Should().Be(type);
        raised.ActorId.Should().Be(account.Id.Value);
    }

    [Fact]
    [Trait("Story", "US-3.1.2-02")]
    [Trait("AC", "AC-04")]
    public void Account_Activate_Employer_RaisesAccountActivated()
    {
        var (account, clock) = PendingWithCode(ActorType.Employer);
        account.ClearDomainEvents();

        account.Activate("123456", Verifier, clock);

        account.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<AccountActivatedDomainEvent>();
    }

    // -------------------------------------------------------------- e-mail verification

    [Fact]
    [Trait("Story", "US-3.1.5-01")]
    [Trait("AC", "AC-02")]
    public void Account_VerifyEmail_WithValidToken_MarksEmailVerified()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Pending(clock: clock);
        account.IssueEmailVerification(TestKit.PlainVerifier.Hash("tok"), clock);

        account.VerifyEmail("tok", Verifier, clock);

        account.EmailVerifiedAtUtc.Should().Be(TestKit.Start);
        account.EmailVerificationTokenHash.Should().BeNull();
    }

    [Fact]
    public void Account_VerifyEmail_WrongOrExpiredToken_Throws()
    {
        var clock = TestKit.Clock();
        var account = TestKit.Pending(clock: clock);
        account.IssueEmailVerification(TestKit.PlainVerifier.Hash("tok"), clock);

        var wrong = () => account.VerifyEmail("nope", Verifier, clock);
        wrong.ShouldBreakRule(AccountRuleCodes.EmailTokenInvalid);

        clock.Advance(TimeSpan.FromHours(25));
        var expired = () => account.VerifyEmail("tok", Verifier, clock);
        expired.ShouldBreakRule(AccountRuleCodes.EmailTokenInvalid);
    }

    [Fact]
    public void Account_VerifyEmail_WithoutIssuedToken_Throws()
    {
        var account = TestKit.Pending();

        var act = () => account.VerifyEmail("tok", Verifier, TestKit.Clock());

        act.ShouldBreakRule(AccountRuleCodes.EmailTokenInvalid);
    }

    [Fact]
    public void Account_IssueEmailVerification_WithoutEmailOrAlreadyVerified_Throws()
    {
        var clock = TestKit.Clock();
        var noEmail = TestKit.Pending(clock: clock, email: null);
        var withoutEmail = () => noEmail.IssueEmailVerification("h", clock);
        withoutEmail.ShouldBreakRule(AccountRuleCodes.EmailRequired);

        var verified = TestKit.Pending(clock: clock);
        verified.IssueEmailVerification(TestKit.PlainVerifier.Hash("t"), clock);
        verified.VerifyEmail("t", Verifier, clock);
        var again = () => verified.IssueEmailVerification("h", clock);
        again.ShouldBreakRule(AccountRuleCodes.EmailAlreadyVerified);
    }
}
