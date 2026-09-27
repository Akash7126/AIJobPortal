using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.Common;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using Microsoft.Extensions.Time.Testing;

namespace JobPlatform.AccountIdentity.Domain.UnitTests;

public class ApiCredentialTests
{
    private static readonly TestKit.PlainVerifier Verifier = TestKit.PlainVerifier.Instance;

    private static ApiCredential Issue(FakeTimeProvider clock, CredentialControls? controls = null, Account? partner = null) =>
        ApiCredential.Issue(partner ?? TestKit.Active(ActorType.ExternalJobSite, clock), "key-1", TestKit.PlainVerifier.Hash("secret"),
            controls ?? new CredentialControls(), null, Guid.NewGuid(), clock);

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public void ApiCredential_Issue_ForActivePartner_AppliesDefaultLimitsAndExpiration()
    {
        var clock = TestKit.Clock();

        var credential = Issue(clock);

        credential.Status.Should().Be(ApiCredentialStatus.Active);
        credential.KeyId.Should().Be("key-1");
        credential.SecretHash.Should().Be("h:secret").And.NotBe("secret");
        credential.Limits.Should().Be(UsageLimits.Default);
        credential.ExpiresAtUtc.Should().Be(TestKit.Start.AddDays(365));
        credential.IpWhitelist.IsEmpty.Should().BeTrue();
        var raised = credential.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ApiCredentialIssuedDomainEvent>().Which;
        raised.ExpiresAtUtc.Should().Be(credential.ExpiresAtUtc);
        raised.PartnerAccountId.Should().Be(credential.PartnerAccountId);
    }

    [Theory]
    [InlineData(ActorType.JobSeeker)]
    [InlineData(ActorType.Employer)]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public void ApiCredential_Issue_ForNonPartner_ThrowsPartnerNotActive(ActorType type)
    {
        var clock = TestKit.Clock();
        var act = () => Issue(clock, partner: TestKit.Active(type, clock));

        act.ShouldBreakRule(ApiCredentialRuleCodes.PartnerNotActive, ErrorCodes.PartnerNotActive, BusinessRuleKind.Forbidden);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-01")]
    public void ApiCredential_Issue_ForPendingPartner_ThrowsPartnerNotActive()
    {
        var clock = TestKit.Clock();
        var act = () => Issue(clock, partner: TestKit.Pending(ActorType.ExternalJobSite, clock));

        act.ShouldBreakRule(ApiCredentialRuleCodes.PartnerNotActive);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-02")]
    public void ApiCredential_Issue_ControlsAreIndependentlyConfigurable()
    {
        var clock = TestKit.Clock();

        var onlyIp = Issue(clock, new CredentialControls(IpWhitelist: new[] { "10.0.0.0/8" }));
        onlyIp.IpWhitelist.Entries.Should().Equal("10.0.0.0/8");
        onlyIp.Limits.Should().Be(UsageLimits.Default);
        onlyIp.ExpiresAtUtc.Should().Be(TestKit.Start.AddDays(365));

        var onlyLimits = Issue(clock, new CredentialControls(Limits: new UsageLimits(50, 60)));
        onlyLimits.Limits.MaxRequests.Should().Be(50);
        onlyLimits.Limits.PeriodSeconds.Should().Be(60);
        onlyLimits.IpWhitelist.IsEmpty.Should().BeTrue();

        var onlyExpiry = Issue(clock, new CredentialControls(ExpiresAtUtc: TestKit.Start.AddDays(30)));
        onlyExpiry.ExpiresAtUtc.Should().Be(TestKit.Start.AddDays(30));
        onlyExpiry.Limits.Should().Be(UsageLimits.Default);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(731)]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-02")]
    public void ApiCredential_Issue_ExpirationOutsideAllowedRange_Throws(int days)
    {
        var clock = TestKit.Clock();
        var act = () => Issue(clock, new CredentialControls(ExpiresAtUtc: TestKit.Start.AddDays(days)));

        act.ShouldBreakRule(ApiCredentialRuleCodes.InvalidExpiry, kind: BusinessRuleKind.InvalidInput);
    }

    [Fact]
    public void ApiCredential_Limits_MustBePositive()
    {
        var zeroRequests = () => new UsageLimits(0, 60);
        var zeroPeriod = () => new UsageLimits(10, 0);
        zeroRequests.ShouldBreakRule(ApiCredentialRuleCodes.InvalidLimits);
        zeroPeriod.ShouldBreakRule(ApiCredentialRuleCodes.InvalidLimits);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-03")]
    public void ApiCredential_Issue_WhileAnotherIsActive_ThrowsOneActivePerPartner()
    {
        var clock = TestKit.Clock();
        var partner = TestKit.Active(ActorType.ExternalJobSite, clock);
        var first = Issue(clock, partner: partner);

        var act = () => ApiCredential.Issue(partner, "key-2", "h", new CredentialControls(), first, Guid.NewGuid(), clock);

        act.ShouldBreakRule(ApiCredentialRuleCodes.OneActivePerPartner, kind: BusinessRuleKind.Conflict);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-03")]
    public void ApiCredential_Issue_AfterRevokingThePrevious_IsAllowed()
    {
        var clock = TestKit.Clock();
        var partner = TestKit.Active(ActorType.ExternalJobSite, clock);
        var first = Issue(clock, partner: partner);
        first.Revoke(Guid.NewGuid(), clock);

        var second = ApiCredential.Issue(partner, "key-2", "h", new CredentialControls(), first, Guid.NewGuid(), clock);

        first.Status.Should().Be(ApiCredentialStatus.Revoked);
        first.RevokedAtUtc.Should().Be(TestKit.Start);
        second.Status.Should().Be(ApiCredentialStatus.Active);
    }

    [Fact]
    public void ApiCredential_Revoke_Twice_ThrowsAlreadyRevoked()
    {
        var clock = TestKit.Clock();
        var credential = Issue(clock);
        credential.Revoke(Guid.NewGuid(), clock);

        var act = () => credential.Revoke(Guid.NewGuid(), clock);

        act.ShouldBreakRule(ApiCredentialRuleCodes.AlreadyRevoked);
        credential.DomainEvents.OfType<ApiCredentialRevokedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-01")]
    public void ApiCredential_Authenticate_WithValidSecret_Succeeds()
    {
        var clock = TestKit.Clock();
        var credential = Issue(clock);

        credential.Authenticate("secret", "203.0.113.5", Verifier, clock);

        credential.FailedAuthAttempts.Should().Be(0);
    }

    [Fact]
    [Trait("Story", "US-3.1.3-02")]
    [Trait("AC", "AC-04")]
    public void ApiCredential_Authenticate_AfterExpiry_ThrowsPartnerExpiredAndMarksExpired()
    {
        var clock = TestKit.Clock();
        var credential = Issue(clock, new CredentialControls(ExpiresAtUtc: TestKit.Start.AddDays(1)));
        clock.Advance(TimeSpan.FromDays(1));

        var act = () => credential.Authenticate("secret", null, Verifier, clock);

        act.ShouldBreakRule(ApiCredentialRuleCodes.Expired, ErrorCodes.PartnerExpired, BusinessRuleKind.Unauthorized);
        credential.Status.Should().Be(ApiCredentialStatus.Expired);
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-02")]
    public void ApiCredential_Authenticate_ExpiredCredential_IsRejectedBeforeAnyCallIsProcessed()
    {
        var clock = TestKit.Clock();
        var credential = Issue(clock, new CredentialControls(ExpiresAtUtc: TestKit.Start.AddMinutes(5)));
        clock.Advance(TimeSpan.FromMinutes(6));

        var act = () => credential.Authenticate("secret", "1.2.3.4", Verifier, clock);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be(ErrorCodes.PartnerExpired);
    }

    [Fact]
    public void ApiCredential_Authenticate_WrongSecret_IsCountedAndDoesNotRevealExpiry()
    {
        var clock = TestKit.Clock();
        var credential = Issue(clock, new CredentialControls(ExpiresAtUtc: TestKit.Start.AddDays(1)));
        clock.Advance(TimeSpan.FromDays(2));

        var act = () => credential.Authenticate("wrong", null, Verifier, clock);

        act.ShouldBreakRule(ApiCredentialRuleCodes.InvalidSecret, ErrorCodes.ApiInvalidClient);
        credential.FailedAuthAttempts.Should().Be(1);
        credential.Status.Should().Be(ApiCredentialStatus.Active, "an unauthenticated caller learns nothing about expiry");
    }

    [Fact]
    [Trait("Story", "US-3.4.3-04")]
    [Trait("AC", "AC-03")]
    public void ApiCredential_Authenticate_SixthAttemptAfterFiveFailures_IsRateLimited()
    {
        var clock = TestKit.Clock();
        var credential = Issue(clock);
        for (var i = 0; i < 5; i++)
        {
            var wrong = () => credential.Authenticate("wrong", null, Verifier, clock);
            wrong.ShouldBreakRule(ApiCredentialRuleCodes.InvalidSecret);
        }

        var act = () => credential.Authenticate("secret", null, Verifier, clock);

        act.ShouldBreakRule(ApiCredentialRuleCodes.RateLimited, ErrorCodes.ApiRateLimited, BusinessRuleKind.RateLimited);

        clock.Advance(TimeSpan.FromMinutes(15));
        credential.Authenticate("secret", null, Verifier, clock);
        credential.FailedAuthAttempts.Should().Be(0);
    }

    [Fact]
    public void ApiCredential_Authenticate_Revoked_IsRejected()
    {
        var clock = TestKit.Clock();
        var credential = Issue(clock);
        credential.Revoke(Guid.NewGuid(), clock);

        var act = () => credential.Authenticate("secret", null, Verifier, clock);

        act.ShouldBreakRule(ApiCredentialRuleCodes.Revoked, ErrorCodes.PartnerRevoked);
    }

    [Fact]
    public void ApiCredential_Authenticate_FromNonWhitelistedIp_ThrowsForbidden()
    {
        var clock = TestKit.Clock();
        var credential = Issue(clock, new CredentialControls(IpWhitelist: new[] { "203.0.113.0/24", "198.51.100.7" }));

        credential.Authenticate("secret", "203.0.113.200", Verifier, clock);
        credential.Authenticate("secret", "198.51.100.7", Verifier, clock);
        var act = () => credential.Authenticate("secret", "192.0.2.1", Verifier, clock);

        act.ShouldBreakRule(ApiCredentialRuleCodes.IpNotAllowed, ErrorCodes.PartnerIpNotAllowed, BusinessRuleKind.Forbidden);
    }

    // -------------------------------------------------------------- IP whitelist value object

    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("192.168.0.1", false)]
    [InlineData("::ffff:10.9.9.9", true)]
    [InlineData(null, false)]
    [InlineData("not-an-ip", false)]
    public void IpWhitelist_Allows_MatchesCidrRanges(string? ip, bool allowed) =>
        IpWhitelist.Create(new[] { "10.0.0.0/8" }).Allows(ip).Should().Be(allowed);

    [Fact]
    public void IpWhitelist_Empty_AllowsEverything()
    {
        IpWhitelist.None.Allows("8.8.8.8").Should().BeTrue();
        IpWhitelist.Create(null).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IpWhitelist_Create_DeduplicatesAndRejectsInvalidEntries()
    {
        IpWhitelist.Create(new[] { "1.1.1.1", "1.1.1.1", " 2.2.2.2 " }).Entries.Should().Equal("1.1.1.1", "2.2.2.2");
        IpWhitelist.Create(new[] { "1.1.1.1" }).Should().Be(IpWhitelist.Create(new[] { "1.1.1.1" }));

        var invalid = () => IpWhitelist.Create(new[] { "999.1.1.1" });
        invalid.ShouldBreakRule(ApiCredentialRuleCodes.InvalidIpWhitelist);
        var badCidr = () => IpWhitelist.Create(new[] { "10.0.0.0/99" });
        badCidr.ShouldBreakRule(ApiCredentialRuleCodes.InvalidIpWhitelist);
    }
}
