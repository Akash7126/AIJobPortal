using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain.UnitTests;

public class NotificationPreferenceTests
{
    private static readonly DateTime T = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Actor Owner = new(Me, false);

    private static NotificationPreference New() => NotificationPreference.Default(Me, T);

    [Fact]
    public void Default_AllowsEverything_Immediate()
    {
        var p = New();

        p.EmailAllowed(Categories.News).Should().BeTrue();
        p.InAppAllowed(Categories.WeeklyRecommendation).Should().BeTrue();
        p.EmailMode.Should().Be(DeliveryMode.Immediate);
        p.SmsOptedIn.Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.6.1-03")]
    [Trait("AC", "AC-01")]
    public void SetEmailPreference_DisablesACategoryAndSetsDigestMode()
    {
        var p = New();

        p.SetEmailPreference(Owner, new Dictionary<string, bool> { ["news"] = false }, DeliveryMode.Digest, T);

        p.EmailAllowed(Categories.News).Should().BeFalse();
        p.EmailAllowed(Categories.SavedSearchMatch).Should().BeTrue();
        p.EmailMode.Should().Be(DeliveryMode.Digest);
    }

    [Theory]
    [Trait("Story", "US-3.6.1-03")]
    [Trait("AC", "AC-02")]
    [InlineData("SecurityAlert")]
    [InlineData("Otp")]
    [InlineData("PasswordReset")]
    public void MandatoryCategories_CannotBeDisabled(string category)
    {
        var p = New();
        var email = () => p.SetEmailPreference(Owner, new Dictionary<string, bool> { [category] = false }, DeliveryMode.Immediate, T);
        var inApp = () => p.SetInAppPreference(Owner, new Dictionary<string, bool> { [category] = false }, T);

        email.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-NOTIF-MANDATORY-CATEGORY");
        inApp.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(NotificationRuleCodes.MandatoryCannotDisable);
        p.EmailAllowed(category).Should().BeTrue();
    }

    [Theory]
    [Trait("Story", "US-3.6.3-02")]
    [Trait("AC", "AC-03")]
    [InlineData("email", "E-EMAILN-FORBIDDEN")]
    [InlineData("in-app", "E-INAPPN-FORBIDDEN")]
    [InlineData("sms", "E-SMSN-FORBIDDEN")]
    public void SomeoneElse_IsForbiddenWithTheChannelsOwnCode(string channel, string code)
    {
        var stranger = new Actor(Guid.NewGuid(), true);
        Action act = channel switch
        {
            "email" => () => New().SetEmailPreference(stranger, new Dictionary<string, bool>(), DeliveryMode.Immediate, T),
            "in-app" => () => New().SetInAppPreference(stranger, new Dictionary<string, bool>(), T),
            _ => () => New().SetSmsOptIn(stranger, "+970591234567", true, T)
        };

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.ExternalCode.Should().Be(code);
        ex.Kind.Should().Be(BusinessRuleKind.Forbidden);
    }

    [Fact]
    [Trait("Story", "US-3.6.3-02")]
    [Trait("AC", "AC-01")]
    public void SmsOptIn_NeedsAValidMobile_AndCanBeWithdrawn()
    {
        var p = New();
        var bad = () => p.SetSmsOptIn(Owner, "12345", true, T);
        bad.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-SMSN-INVALID-FIELD");

        p.SetSmsOptIn(Owner, "+970 59 123 4567", true, T);
        p.SmsOptedIn.Should().BeTrue();
        p.Mobile.Should().Be("+970591234567");

        p.SetSmsOptIn(Owner, null, false, T);
        p.SmsOptedIn.Should().BeFalse();
    }

    [Fact]
    [Trait("Story", "US-3.6.1-05")]
    [Trait("AC", "AC-02")]
    public void Unsubscribe_IsHonoured_AndReEnablingClearsIt()
    {
        var p = New();
        p.Unsubscribe("weeklyrecommendation", T);

        p.EmailAllowed(Categories.WeeklyRecommendation).Should().BeFalse();
        p.IsUnsubscribed(Categories.WeeklyRecommendation).Should().BeTrue();

        p.SetEmailPreference(Owner, new Dictionary<string, bool> { [Categories.WeeklyRecommendation] = true }, DeliveryMode.Immediate, T);

        p.EmailAllowed(Categories.WeeklyRecommendation).Should().BeTrue();
    }

    [Fact]
    public void Unsubscribe_FromMandatoryOrUnknownCategory_IsRefused()
    {
        var mandatory = () => New().Unsubscribe(Categories.SecurityAlert, T);
        var unknown = () => New().Unsubscribe("Nonsense", T);

        mandatory.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-NOTIF-MANDATORY-CATEGORY");
        unknown.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void UnknownCategory_InASetting_IsRefused()
    {
        var act = () => New().SetInAppPreference(Owner, new Dictionary<string, bool> { ["Nonsense"] = true }, T);

        act.Should().Throw<BusinessRuleViolationException>().Which.Kind.Should().Be(BusinessRuleKind.InvalidInput);
    }
}

public class ChannelRouterTests
{
    private static readonly DateTime T = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Owner = new(Guid.NewGuid(), false);
    private static readonly SmsPolicy Policy = SmsPolicy.Initial(T);

    private static NotificationPreference Pref() => NotificationPreference.Default(Owner.Id, T);

    private static readonly Channel[] All = { Channel.Email, Channel.Sms, Channel.InApp };

    [Fact]
    [Trait("Story", "US-3.6.3-03")]
    [Trait("AC", "AC-01")]
    public void NonEssentialSms_IsNotSent_AndFallsBackToEmailAndInApp()
    {
        var decisions = ChannelRouter.Route(Categories.SavedSearchMatch, new[] { Channel.Sms }, Pref(), Policy);

        decisions.Single(d => d.Channel == Channel.Sms).Should().Match<ChannelDecision>(d => !d.Send && d.Reason == ChannelRouter.NonEssentialRerouted);
        decisions.Single(d => d.Channel == Channel.Email).Send.Should().BeTrue();
        decisions.Single(d => d.Channel == Channel.InApp).Send.Should().BeTrue();
    }

    [Theory]
    [InlineData("Otp")]
    [InlineData("PasswordReset")]
    public void EssentialSms_IsAlwaysSent_EvenWithoutOptIn(string category) =>
        ChannelRouter.Route(category, new[] { Channel.Sms }, Pref(), Policy).Should().ContainSingle().Which.Send.Should().BeTrue();

    [Fact]
    public void UnsubscribedEmail_IsSuppressedWithTheUnsubscribeReason_AndPreferenceOffWithPreferenceReason()
    {
        var p = Pref();
        p.Unsubscribe(Categories.News, T);
        var unsub = ChannelRouter.Route(Categories.News, new[] { Channel.Email }, p, Policy).Single();
        unsub.Should().Match<ChannelDecision>(d => !d.Send && d.Reason == ChannelRouter.Unsubscribed);

        var q = Pref();
        q.SetEmailPreference(Owner, new Dictionary<string, bool> { [Categories.Welcome] = false }, DeliveryMode.Immediate, T);
        ChannelRouter.Route(Categories.Welcome, new[] { Channel.Email }, q, Policy).Single().Reason.Should().Be(ChannelRouter.Preference);
    }

    [Fact]
    public void MandatoryCategory_IsNeverSuppressedByPreferences()
    {
        var p = Pref();
        p.Suspend(T);
        p.Unsubscribe(Categories.News, T);

        var decisions = ChannelRouter.Route(Categories.SecurityAlert, All, NotificationPreference.Default(Owner.Id, T), Policy);

        decisions.Where(d => d.Channel != Channel.Sms).Should().OnlyContain(d => d.Send);
    }

    [Fact]
    public void DigestMode_MarksNonMandatoryEmailAsDigest()
    {
        var p = Pref();
        p.SetEmailPreference(Owner, new Dictionary<string, bool>(), DeliveryMode.Digest, T);

        ChannelRouter.Route(Categories.News, new[] { Channel.Email }, p, Policy).Single().AsDigest.Should().BeTrue();
        ChannelRouter.Route(Categories.SecurityAlert, new[] { Channel.Email }, p, Policy).Single().AsDigest.Should().BeFalse();
    }

    [Fact]
    public void SuspendedAccount_GetsNothing()
    {
        var p = Pref();
        p.Suspend(T);

        ChannelRouter.Route(Categories.Welcome, All, p, Policy).Should().OnlyContain(d => !d.Send && d.Reason == ChannelRouter.AccountSuspended);
    }

    [Fact]
    public void InAppPreferenceOff_SuppressesInApp()
    {
        var p = Pref();
        p.SetInAppPreference(Owner, new Dictionary<string, bool> { [Categories.News] = false }, T);

        ChannelRouter.Route(Categories.News, new[] { Channel.InApp }, p, Policy).Single().Send.Should().BeFalse();
    }
}
