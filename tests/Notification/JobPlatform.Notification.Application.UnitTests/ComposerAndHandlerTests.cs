using JobPlatform.Notification.Application.Composition;
using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using Microsoft.Extensions.Time.Testing;

namespace JobPlatform.Notification.Application.UnitTests;

public class NotificationComposerTests
{
    private readonly FakeStore _store = new();
    private readonly FakeTimeProvider _clock = Clocks.At();
    private static readonly Guid User = Guid.NewGuid();

    private ComposeRequest Request(string category = Categories.SavedSearchMatch, string source = "m1", Channel[]? channels = null, string? template = null,
        bool secret = false) =>
        new(User, category, "saved-search-match", channels ?? new[] { Channel.InApp, Channel.Email }, source, new LocalizedText("ع", "Title"), new LocalizedText("ن", "Body"), "/jobs/1",
            template, new Dictionary<string, string>(), secret);

    [Fact]
    [Trait("Story", "US-3.6.1-01")]
    [Trait("AC", "AC-01")]
    public async Task Compose_CreatesInAppAndEmail_WithAnUnsubscribeLinkOnMarketingMail()
    {
        var result = await _store.Composer(_clock).ComposeAsync(Request(), default);

        result.Created.Should().BeEquivalentTo(new[] { Channel.Email, Channel.InApp });
        _store.InApp.Should().ContainSingle().Which.Status.Should().Be(InAppStatus.Unread);
        var email = _store.Messages.Should().ContainSingle().Which;
        email.Status.Should().Be(MessageStatus.Pending);
        email.Body.Should().Contain("/unsubscribe/tok-");
    }

    [Fact]
    [Trait("Story", "US-3.6.1-01")]
    [Trait("AC", "AC-03")]
    public async Task Compose_TheSameSourceMessageTwice_NeverYieldsASecondEmail()
    {
        var composer = _store.Composer(_clock);

        await composer.ComposeAsync(Request(), default);
        var second = await composer.ComposeAsync(Request(), default);

        _store.Messages.Should().ContainSingle();
        second.WasDuplicate.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.6.1-05")]
    [Trait("AC", "AC-02")]
    public async Task Compose_ForAnUnsubscribedCategory_KeepsASuppressedRecordAndSendsNothing()
    {
        var preference = NotificationPreference.Default(User, _clock.GetUtcNow().UtcDateTime);
        preference.Unsubscribe(Categories.SavedSearchMatch, _clock.GetUtcNow().UtcDateTime);
        _store.Preferences.Add(preference);

        var result = await _store.Composer(_clock).ComposeAsync(Request(channels: new[] { Channel.Email }), default);

        result.Created.Should().BeEmpty();
        _store.Messages.Should().ContainSingle().Which.Should().Match<OutboundMessage>(m => m.Status == MessageStatus.Suppressed && m.SuppressionReason == ChannelRouter.Unsubscribed);
    }

    [Fact]
    public async Task Compose_WithDigestMode_MarksTheEmailAsDigestCandidate()
    {
        var preference = NotificationPreference.Default(User, _clock.GetUtcNow().UtcDateTime);
        preference.SetEmailPreference(new Actor(User, false), new Dictionary<string, bool>(), DeliveryMode.Digest, _clock.GetUtcNow().UtcDateTime);
        _store.Preferences.Add(preference);

        await _store.Composer(_clock).ComposeAsync(Request(channels: new[] { Channel.Email }), default);

        _store.Messages.Single().IsDigestCandidate.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.6.3-03")]
    [Trait("AC", "AC-01")]
    public async Task Compose_NonEssentialSms_IsSuppressedAndRoutedToEmailAndInApp()
    {
        var result = await _store.Composer(_clock).ComposeAsync(Request(channels: new[] { Channel.Sms }), default);

        result.Created.Should().BeEquivalentTo(new[] { Channel.Email, Channel.InApp });
        result.Suppressed.Should().ContainSingle().Which.Should().Be((Channel.Sms, ChannelRouter.NonEssentialRerouted));
        _store.Messages.Should().Contain(m => m.Channel == Channel.Sms && m.Status == MessageStatus.Suppressed);
    }

    [Fact]
    [Trait("Story", "US-3.6.3-01")]
    [Trait("AC", "AC-01")]
    public async Task Compose_OtpSms_IsQueuedWithASecretBody()
    {
        var result = await _store.Composer(_clock).ComposeAsync(Request(Categories.Otp, channels: new[] { Channel.Sms }, secret: true), default);

        result.Created.Should().ContainSingle().Which.Should().Be(Channel.Sms);
        _store.Messages.Single().ContainsSecret.Should().BeTrue();
    }

    [Fact]
    [Trait("Story", "US-3.6.3-06")]
    [Trait("AC", "AC-03")]
    public async Task Compose_SmsWithAnUnapprovedSenderId_IsSuppressedForCompliance()
    {
        var composer = _store.Composer(_clock, new NotificationOptions { SmsSenderId = "Rogue", ApprovedSmsSenderIds = new[] { "JobPlatform" } });

        var result = await composer.ComposeAsync(Request(Categories.Otp, channels: new[] { Channel.Sms }), default);

        result.Created.Should().BeEmpty();
        _store.Messages.Single().SuppressionReason.Should().Be(NotificationRuleCodes.ComplianceViolation);
    }

    [Fact]
    [Trait("Story", "US-3.6.1-02")]
    [Trait("AC", "AC-01")]
    public async Task Compose_WithATemplate_RendersItsCurrentVersion()
    {
        _store.Templates.Add(EmailTemplate.Create("welcome", "en", "Hi {{name}}", "Welcome {{name}}", new Dictionary<string, string> { ["name"] = "friend" }, _clock.GetUtcNow().UtcDateTime));

        await _store.Composer(_clock).ComposeAsync(Request(Categories.Welcome, channels: new[] { Channel.Email }, template: "welcome"), default);

        var email = _store.Messages.Single();
        email.Subject.Should().Be("Hi friend");
        email.TemplateVersion.Should().Be(1);
    }

    [Fact]
    public async Task Compose_ForASuspendedAccount_SendsNothing()
    {
        var preference = NotificationPreference.Default(User, _clock.GetUtcNow().UtcDateTime);
        preference.Suspend(_clock.GetUtcNow().UtcDateTime);
        _store.Preferences.Add(preference);

        var result = await _store.Composer(_clock).ComposeAsync(Request(), default);

        result.Created.Should().BeEmpty();
        _store.InApp.Should().BeEmpty();
    }
}

public class InboxHandlerTests
{
    private readonly FakeStore _store = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = Clocks.At();
    private static readonly Guid User = Guid.NewGuid();

    [Fact]
    [Trait("Story", "US-3.6.2-02")]
    [Trait("AC", "AC-01")]
    public async Task SavedSearchMatched_NotifiesTheJobSeekerInAppAndByEmail()
    {
        await new NotifySavedSearchMatchHandler(_store.Composer(_clock)).Handle(
            new SavedSearchMatchedIntegrationEvent(Guid.NewGuid(), _clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), null, Guid.NewGuid(), User, Guid.NewGuid(), "Engineer", 1), default);

        _store.InApp.Single().RecipientAccountId.Should().Be(User);
        _store.InApp.Single().TitleEn.Should().Contain("Engineer");
        _store.Messages.Single().Channel.Should().Be(Channel.Email);
    }

    [Fact]
    public async Task SavedSearchMatched_Redelivered_DoesNotDuplicateTheEmail()
    {
        var handler = new NotifySavedSearchMatchHandler(_store.Composer(_clock));
        var evt = new SavedSearchMatchedIntegrationEvent(Guid.NewGuid(), _clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), null, Guid.NewGuid(), User, Guid.NewGuid(), "Engineer", 1);

        await handler.Handle(evt, default);
        await handler.Handle(evt, default);

        _store.Messages.Should().ContainSingle();
    }

    private JobRecommendationComputedIntegrationEvent Recommendation() =>
        new(Guid.NewGuid(), _clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), User, new[] { Guid.NewGuid(), Guid.NewGuid() }, "hybrid", 1);

    [Fact]
    [Trait("Story", "US-3.6.2-03")]
    [Trait("AC", "AC-03")]
    public async Task WeeklyRecommendation_IsSentOncePerIsoWeek_AndAgainNextWeek()
    {
        var handler = new SendWeeklyRecommendationHandler(_store.Composer(_clock), _store, _store, _clock);

        await handler.Handle(Recommendation(), default);
        await handler.Handle(Recommendation(), default);
        _store.InApp.Should().ContainSingle();
        _store.Cycles.Should().ContainSingle();

        _clock.Advance(TimeSpan.FromDays(7));
        await handler.Handle(Recommendation(), default);

        _store.InApp.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Story", "US-3.6.2-03")]
    [Trait("AC", "AC-02")]
    public async Task WeeklyRecommendation_WhenTheCategoryIsDisabled_SendsNothing_AndKeepsTheWeekOpen()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var preference = NotificationPreference.Default(User, now);
        preference.SetInAppPreference(new Actor(User, false), new Dictionary<string, bool> { [Categories.WeeklyRecommendation] = false }, now);
        preference.SetEmailPreference(new Actor(User, false), new Dictionary<string, bool> { [Categories.WeeklyRecommendation] = false }, DeliveryMode.Immediate, now);
        _store.Preferences.Add(preference);

        await new SendWeeklyRecommendationHandler(_store.Composer(_clock), _store, _store, _clock).Handle(Recommendation(), default);

        _store.InApp.Should().BeEmpty();
        _store.Messages.Should().BeEmpty();
        _store.Cycles.Should().BeEmpty();
    }

    private JobDataImportedIntegrationEvent Imported(string platformJobId = "PJ-1", bool update = false) =>
        new(Guid.NewGuid(), _clock.GetUtcNow().UtcDateTime, Guid.NewGuid(), null, Guid.NewGuid(), Guid.Parse("11111111-1111-1111-1111-111111111111"), User, platformJobId, "src-9", "T", "S",
            new[] { "C#" }, "FullTime", "OnSite", null, "Gaza", null, "Visible", update, 1);

    [Fact]
    [Trait("Story", "US-3.1.3-08")]
    [Trait("AC", "AC-02")]
    public async Task JobDataImported_ConfirmsOnce_AndARePushNeverConfirmsAgain()
    {
        var handler = new SendJobConfirmationHandler(_store, _store.Composer(_clock), _clock);

        await handler.Handle(Imported(), default);
        await handler.Handle(Imported(update: true), default);

        _store.Confirmations.Should().ContainSingle().Which.PlatformJobId.Should().Be("PJ-1");
        _store.Messages.Should().ContainSingle().Which.Category.Should().Be(Categories.JobConfirmation);
    }

    [Fact]
    public async Task AccountApproved_SendsAWelcome_AndAccountSuspendedSilencesTheUser()
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        await new SendWelcomeHandler(_store.Composer(_clock)).Handle(new AccountApprovedIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, User, User,
            SharedKernel.Common.Enums.ActorType.JobSeeker, 1), default);
        _store.InApp.Should().ContainSingle();

        await new SuppressRecipientHandler(_store, _clock).Handle(new AccountSuspendedIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, User, User,
            SharedKernel.Common.Enums.ActorType.JobSeeker, "policy", "Deactivated", 2), default);
        await new SendWelcomeHandler(_store.Composer(_clock)).Handle(new AccountApprovedIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, User, User,
            SharedKernel.Common.Enums.ActorType.JobSeeker, 3), default);

        _store.Preferences.Single().Suspended.Should().BeTrue();
        _store.InApp.Should().ContainSingle("nothing is sent to a deactivated account");
    }
}
