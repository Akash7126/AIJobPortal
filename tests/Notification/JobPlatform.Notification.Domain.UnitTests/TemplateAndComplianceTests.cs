using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain.UnitTests;

public class EmailTemplateTests
{
    private static readonly DateTime T = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    private static EmailTemplate New() => EmailTemplate.Create("welcome", "en", "Hi {{name}}", "Dear {{name}}, your code is {{code}}.",
        new Dictionary<string, string> { ["name"] = "there", ["code"] = "n/a" }, T);

    [Fact]
    [Trait("Story", "US-3.6.1-02")]
    [Trait("AC", "AC-01")]
    public void Render_FillsPlaceholders()
    {
        var (subject, body) = New().Render(new Dictionary<string, string> { ["name"] = "Sara", ["code"] = "123" });

        subject.Should().Be("Hi Sara");
        body.Should().Be("Dear Sara, your code is 123.");
    }

    [Fact]
    [Trait("Story", "US-3.6.1-02")]
    [Trait("AC", "AC-02")]
    public void Render_MissingOrBlankValue_FallsBackToTheDefault_NeverALeftoverToken()
    {
        var (subject, body) = New().Render(new Dictionary<string, string> { ["name"] = " " });

        subject.Should().Be("Hi there");
        body.Should().NotContain("{{").And.Contain("n/a");
    }

    [Fact]
    [Trait("Story", "US-3.6.1-02")]
    [Trait("AC", "AC-03")]
    public void NewVersion_IncrementsAndLeavesTheOldOneUntouched()
    {
        var v1 = New();

        var v2 = v1.NewVersion(Admin, "New {{name}}", "Body {{name}}", new Dictionary<string, string> { ["name"] = "you" }, T.AddDays(1));

        v2.Version.Should().Be(2);
        v2.EditedBy.Should().Be(Admin.Id);
        v1.Version.Should().Be(1);
        v1.Subject.Should().Be("Hi {{name}}");
    }

    [Fact]
    [Trait("Story", "US-3.6.1-02")]
    [Trait("AC", "AC-04")]
    public void NewVersion_ByANonAdministrator_IsForbidden()
    {
        var act = () => New().NewVersion(new Actor(Guid.NewGuid(), false), "s", "b", new Dictionary<string, string>(), T);

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-EMAILN-FORBIDDEN");
    }

    [Fact]
    public void Create_WithAPlaceholderWithoutDefault_IsRefused()
    {
        var act = () => EmailTemplate.Create("x", "en", "Hi {{name}}", "Body", new Dictionary<string, string>(), T);

        act.Should().Throw<BusinessRuleViolationException>().Which.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }
}

public class NotificationTypeAndPolicyTests
{
    private static readonly DateTime T = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Admin = new(Guid.NewGuid(), true);

    [Fact]
    [Trait("Story", "US-3.6.2-06")]
    [Trait("AC", "AC-01")]
    public void Define_ByAdministrator_WithAccessibleColour_Works() =>
        NotificationType.Define(Admin, "promo", "star", "#1D4ED8", "Promotion", false).Icon.Should().Be("star");

    [Fact]
    [Trait("Story", "US-3.6.2-06")]
    [Trait("AC", "AC-03")]
    public void Define_ByAnyoneElse_IsForbidden()
    {
        var act = () => NotificationType.Define(new Actor(Guid.NewGuid(), false), "promo", "star", "#1D4ED8", "Promotion", false);

        act.Should().Throw<BusinessRuleViolationException>().Which.Kind.Should().Be(BusinessRuleKind.Forbidden);
    }

    [Theory]
    [InlineData("nope", "#1D4ED8", "Alt")]   // icon not in allow-list
    [InlineData("star", "#FFFF00", "Alt")]   // yellow on white: low contrast
    [InlineData("star", "red", "Alt")]       // not a hex colour
    [InlineData("star", "#1D4ED8", " ")]     // text alternative required
    public void Define_WithInvalidIconColourOrAlternative_IsRefused(string icon, string colour, string alt)
    {
        var act = () => NotificationType.Define(Admin, "promo", icon, colour, alt, false);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(NotificationRuleCodes.InvalidType);
    }

    [Fact]
    [Trait("Story", "US-3.6.2-06")]
    [Trait("AC", "AC-02")]
    public void GenericType_IsAlwaysUsable() => NotificationType.Generic.Should().Match<NotificationType>(t => t.Icon == "bell" && t.TextAlternative != "");

    [Fact]
    public void Contrast_OfBlackIsMaximum_OfWhiteIsMinimum()
    {
        Contrast.OnWhite("#000000").Should().BeApproximately(21, 0.01);
        Contrast.OnWhite("#FFFFFF").Should().BeApproximately(1, 0.01);
    }

    [Fact]
    [Trait("Story", "US-3.6.3-03")]
    [Trait("AC", "AC-02")]
    public void SmsPolicy_NewVersion_KeepsOtpAndResetEssential_AndTheOldVersionIsUnchanged()
    {
        var v1 = SmsPolicy.Initial(T);

        var v2 = v1.NewVersion(Admin, new[] { "otp", "passwordreset", "SecurityAlert" }, T);

        v2.Id.Should().Be(2);
        v2.IsEssential(Categories.SecurityAlert).Should().BeTrue();
        v1.IsEssential(Categories.SecurityAlert).Should().BeFalse("a running batch keeps the list in effect when it started");
    }

    [Fact]
    public void SmsPolicy_RemovingOtp_OrUnknownCategory_OrNonAdmin_IsRefused()
    {
        var removeOtp = () => SmsPolicy.Initial(T).NewVersion(Admin, new[] { "PasswordReset" }, T);
        var unknown = () => SmsPolicy.Initial(T).NewVersion(Admin, new[] { "Otp", "PasswordReset", "Nonsense" }, T);
        var nonAdmin = () => SmsPolicy.Initial(T).NewVersion(new Actor(Guid.NewGuid(), false), new[] { "Otp", "PasswordReset" }, T);

        removeOtp.Should().Throw<BusinessRuleViolationException>();
        unknown.Should().Throw<BusinessRuleViolationException>();
        nonAdmin.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-SMSN-FORBIDDEN");
    }
}

public class ComplianceTests
{
    [Fact]
    [Trait("Story", "US-3.6.1-05")]
    [Trait("AC", "AC-01")]
    public void MarketingEmail_NeedsAnUnsubscribeLink_TransactionalDoesNot()
    {
        var marketing = () => ComplianceGuard.CheckEmail(Categories.News, "Big news", true);
        marketing.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(NotificationRuleCodes.ComplianceViolation);

        ComplianceGuard.CheckEmail(Categories.News, "Big news\nhttps://x/unsubscribe/abc", true);
        ComplianceGuard.CheckEmail(Categories.Welcome, "Hello", true);
    }

    [Fact]
    [Trait("Story", "US-3.6.1-05")]
    [Trait("AC", "AC-03")]
    public void Email_FromAnUnverifiedSenderDomain_IsRefused()
    {
        var act = () => ComplianceGuard.CheckEmail(Categories.Welcome, "Hello", false);

        act.Should().Throw<BusinessRuleViolationException>();
    }

    [Theory]
    [InlineData("Your code is 123456", 1)]
    [InlineData("مرحبا", 1)]
    public void Segments_ShortMessages_AreOneSegment(string body, int expected) => ComplianceGuard.Segments(body).Should().Be(expected);

    [Fact]
    public void Segments_CountGsmAndUnicodeLimits()
    {
        ComplianceGuard.Segments(new string('a', 160)).Should().Be(1);
        ComplianceGuard.Segments(new string('a', 161)).Should().Be(2);
        ComplianceGuard.Segments(new string('ب', 70)).Should().Be(1);
        ComplianceGuard.Segments(new string('ب', 71)).Should().Be(2);
        ComplianceGuard.Segments(string.Empty).Should().Be(0);
    }

    [Fact]
    [Trait("Story", "US-3.6.3-06")]
    [Trait("AC", "AC-01")]
    public void Sms_NeedsAnApprovedSenderId()
    {
        var approved = new[] { "JobPlatform" };
        ComplianceGuard.CheckSms("Hello", "JobPlatform", approved);

        var unknown = () => ComplianceGuard.CheckSms("Hello", "Spammer", approved);
        var none = () => ComplianceGuard.CheckSms("Hello", null, approved);

        unknown.Should().Throw<BusinessRuleViolationException>();
        none.Should().Throw<BusinessRuleViolationException>();
    }

    [Theory]
    [Trait("Story", "US-3.6.3-06")]
    [Trait("AC", "AC-03")]
    [InlineData("Win at the casino today")]
    [InlineData("Free LOTTERY tickets")]
    public void Sms_WithRestrictedContent_IsRefused(string body)
    {
        var act = () => ComplianceGuard.CheckSms(body, "JobPlatform", new[] { "JobPlatform" });

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-SMSN-INVALID-FIELD");
    }

    [Fact]
    [Trait("Story", "US-3.6.3-06")]
    [Trait("AC", "AC-04")]
    public void Sms_TooManySegments_IsRefused()
    {
        var act = () => ComplianceGuard.CheckSms(new string('a', 160 * 8), "JobPlatform", new[] { "JobPlatform" });

        act.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-SMSN-INVALID-FIELD");
    }
}

public class ConfirmationAndWeeklyTests
{
    private static readonly DateTime T = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Story", "US-3.1.3-08")]
    [Trait("AC", "AC-01")]
    public void Confirm_RecordsTheMappingAndRaisesTheEvent()
    {
        var c = JobConfirmation.Confirm(Guid.NewGuid(), "src-1", "PJ-1", Guid.NewGuid(), T);

        c.PlatformJobId.Should().Be("PJ-1");
        c.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<JobConfirmationSentDomainEvent>();
    }

    [Fact]
    public void Confirm_WithoutAJobId_IsRefused()
    {
        var act = () => JobConfirmation.Confirm(Guid.NewGuid(), "src-1", "", Guid.NewGuid(), T);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(NotificationRuleCodes.InvalidConfirmation);
    }

    [Fact]
    [Trait("Story", "US-3.6.2-03")]
    [Trait("AC", "AC-03")]
    public void IsoWeek_IsStableWithinAWeekAndChangesAcrossWeeks()
    {
        DedupeKeyFactory.IsoWeek(new DateTime(2026, 5, 4)).Should().Be(DedupeKeyFactory.IsoWeek(new DateTime(2026, 5, 10)));
        DedupeKeyFactory.IsoWeek(new DateTime(2026, 5, 10)).Should().NotBe(DedupeKeyFactory.IsoWeek(new DateTime(2026, 5, 11)));
        DedupeKeyFactory.IsoWeek(new DateTime(2026, 1, 1)).Should().Be("2026-W01");
        DedupeKeyFactory.IsoWeek(new DateTime(2027, 1, 1)).Should().Be("2026-W53");
    }

    [Fact]
    public void DedupeKey_IsSpecificToSourceRecipientCategoryAndChannel()
    {
        var recipient = Guid.NewGuid();

        var a = DedupeKeyFactory.For("m1", recipient, "News", Channel.Email);

        a.Should().Be(DedupeKeyFactory.For("m1", recipient, "News", Channel.Email));
        a.Should().NotBe(DedupeKeyFactory.For("m1", recipient, "News", Channel.Sms));
        a.Should().NotBe(DedupeKeyFactory.For("m2", recipient, "News", Channel.Email));
    }
}
