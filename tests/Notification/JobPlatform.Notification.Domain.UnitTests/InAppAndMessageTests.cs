using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain.UnitTests;

public class InAppNotificationTests
{
    private static readonly DateTime T = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Me = Guid.NewGuid();

    private static InAppNotification New() =>
        InAppNotification.Create(Me, "saved-search-match", Categories.SavedSearchMatch, new LocalizedText("عنوان", "Title"), new LocalizedText("نص", "Body"), "/jobs/1", T);

    [Fact]
    [Trait("Story", "US-3.6.2-02")]
    [Trait("AC", "AC-01")]
    public void Create_StartsUnread_AndRaisesCreatedEvent()
    {
        var n = New();

        n.Status.Should().Be(InAppStatus.Unread);
        n.RecipientAccountId.Should().Be(Me);
        n.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<InAppNotificationCreatedDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.6.2-07")]
    [Trait("AC", "AC-01")]
    public void MarkRead_ByRecipient_MovesToReadOnce_AndRaisesStatusEvent()
    {
        var n = New();
        n.ClearDomainEvents();

        n.MarkRead(new Actor(Me, false), T.AddMinutes(1));
        n.MarkRead(new Actor(Me, false), T.AddMinutes(2));

        n.Status.Should().Be(InAppStatus.Read);
        n.ReadAtUtc.Should().Be(T.AddMinutes(1));
        n.DomainEvents.Should().ContainSingle().Which.Should().Match<InAppNotificationStatusChangedDomainEvent>(e => e.From == InAppStatus.Unread && e.To == InAppStatus.Read);
    }

    [Fact]
    public void MarkRead_ByAnotherUser_IsForbiddenWithInAppCode()
    {
        var act = () => New().MarkRead(new Actor(Guid.NewGuid(), false), T);

        var ex = act.Should().Throw<BusinessRuleViolationException>().Which;
        ex.Code.Should().Be(NotificationRuleCodes.NotRecipient);
        ex.ExternalCode.Should().Be("E-INAPPN-FORBIDDEN");
    }

    [Fact]
    [Trait("Story", "US-3.6.2-07")]
    [Trait("AC", "AC-03")]
    public void Delete_ThenAnyChange_IsNotFound()
    {
        var n = New();
        n.Delete(new Actor(Me, false), T);

        var read = () => n.MarkRead(new Actor(Me, false), T);
        var delete = () => n.Delete(new Actor(Me, false), T);

        read.Should().Throw<BusinessRuleViolationException>().Which.ExternalCode.Should().Be("E-INAPPN-NOT-FOUND");
        delete.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(NotificationRuleCodes.AlreadyDeleted);
    }

    [Fact]
    [Trait("Story", "US-3.6.2-07")]
    [Trait("AC", "AC-02")]
    public void TakeAction_MarksReadAndReturnsTheLink() =>
        New().Also(n => n.TakeAction(new Actor(Me, false), T).Should().Be("/jobs/1")).Status.Should().Be(InAppStatus.Read);

    [Fact]
    public void Create_WithoutRecipientOrTitle_Throws()
    {
        var noRecipient = () => InAppNotification.Create(Guid.Empty, "t", "c", new LocalizedText("a", "b"), new LocalizedText("a", "b"), null, T);
        var noTitle = () => InAppNotification.Create(Me, "t", "c", new LocalizedText("", ""), new LocalizedText("a", "b"), null, T);

        noRecipient.Should().Throw<BusinessRuleViolationException>();
        noTitle.Should().Throw<BusinessRuleViolationException>();
    }
}

internal static class TestExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}

public class OutboundMessageTests
{
    private static readonly DateTime T = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    private static OutboundMessage New(Channel channel = Channel.Email, bool secret = false, bool digest = false) =>
        OutboundMessage.Compose(channel, Guid.NewGuid(), Categories.Welcome, "key-1", "Hi", "Body", "en", null, null, digest, secret, T);

    [Fact]
    public void Compose_StartsPendingAndDue()
    {
        var m = New();

        m.Status.Should().Be(MessageStatus.Pending);
        m.DeliveryStatus.Should().Be(DeliveryStatus.Pending);
        m.IsDue(T).Should().BeTrue();
        m.IsDue(T.AddSeconds(-1)).Should().BeFalse();
    }

    [Fact]
    public void Compose_ForInApp_IsRefused()
    {
        var act = () => OutboundMessage.Compose(Channel.InApp, Guid.NewGuid(), "c", "k", "s", "b", "en", null, null, false, false, T);

        act.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(NotificationRuleCodes.InvalidMessage);
    }

    [Fact]
    [Trait("Story", "US-3.6.1-01")]
    [Trait("AC", "AC-01")]
    public void SendFlow_MarksSent_RaisesEvent_AndDeliveryStaysPendingUntilReported()
    {
        var m = New(Channel.Sms);
        m.BeginSending();
        m.MarkSent("prov-1", "+970****567", "JobPlatform", T);

        m.Status.Should().Be(MessageStatus.Sent);
        m.Attempts.Should().Be(1);
        m.DeliveryStatus.Should().Be(DeliveryStatus.Pending, "delivery is never assumed (3.6.3-04 AC-02)");
        m.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<OutboundMessageSentDomainEvent>();
    }

    [Fact]
    [Trait("Story", "US-3.6.1-01")]
    [Trait("AC", "AC-04")]
    public void Failure_RetriesThreeTimesWithBackoff_ThenFailsWithChannelTimeoutCode()
    {
        var m = New();
        var now = T;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            m.BeginSending();
            m.RecordFailure("E-EMAILN-UPSTREAM-TIMEOUT", now);
            m.Status.Should().Be(MessageStatus.Pending, $"retry {attempt} remains");
            m.NextAttemptUtc.Should().BeAfter(now);
            now = m.NextAttemptUtc;
        }

        m.BeginSending();
        m.RecordFailure("E-EMAILN-UPSTREAM-TIMEOUT", now);

        m.Status.Should().Be(MessageStatus.Failed);
        m.ErrorCode.Should().Be("E-EMAILN-UPSTREAM-TIMEOUT");
        m.DeliveryStatus.Should().Be(DeliveryStatus.Failed);
        OutboundMessage.TimeoutCode(Channel.Sms).Should().Be("E-SMSN-UPSTREAM-TIMEOUT");
    }

    [Fact]
    [Trait("Story", "US-3.6.3-04")]
    [Trait("AC", "AC-02")]
    public void DeliveryReport_OnlyAfterSent_AndFinalReportIsNotOverwritten()
    {
        var m = New(Channel.Sms);
        m.RecordDeliveryReport(DeliveryStatus.Delivered).Should().BeFalse("not sent yet");
        m.BeginSending();
        m.MarkSent("p", "m", null, T);

        m.RecordDeliveryReport(DeliveryStatus.Pending).Should().BeFalse();
        m.RecordDeliveryReport(DeliveryStatus.Delivered).Should().BeTrue();
        m.RecordDeliveryReport(DeliveryStatus.Failed).Should().BeFalse();
        m.DeliveryStatus.Should().Be(DeliveryStatus.Delivered);
    }

    [Fact]
    public void SecretBody_IsWipedOnceSentOrFailedForGood()
    {
        var sent = New(Channel.Sms, secret: true);
        sent.BeginSending();
        sent.MarkSent("p", "m", null, T);
        sent.Body.Should().BeNull();

        var failed = New(Channel.Sms, secret: true);
        for (var i = 0; i <= OutboundMessage.MaxRetries; i++)
        {
            failed.BeginSending();
            failed.RecordFailure("x", T);
        }

        failed.Status.Should().Be(MessageStatus.Failed);
        failed.Body.Should().BeNull();
    }

    [Fact]
    public void Transitions_OutOfOrder_AreRefused()
    {
        var m = New();

        var sentWithoutSending = () => m.MarkSent("p", "m", null, T);
        var failWithoutSending = () => m.RecordFailure("x", T);

        sentWithoutSending.Should().Throw<BusinessRuleViolationException>().Which.Code.Should().Be(NotificationRuleCodes.InvalidTransition);
        failWithoutSending.Should().Throw<BusinessRuleViolationException>();
    }

    [Fact]
    public void Suppressed_KeepsTheReason_AndIsNeverDue()
    {
        var m = OutboundMessage.Suppressed(Channel.Email, Guid.NewGuid(), "News", "k", "NT.Email.UNSUBSCRIBED", T);

        m.Status.Should().Be(MessageStatus.Suppressed);
        m.SuppressionReason.Should().Be("NT.Email.UNSUBSCRIBED");
        m.IsDue(T.AddDays(1)).Should().BeFalse();
    }

    [Fact]
    public void DigestCandidate_IsNotDue_AndCanBeDigestedOnce()
    {
        var m = New(digest: true);
        m.IsDue(T.AddDays(1)).Should().BeFalse();

        var digest = Guid.NewGuid();
        m.MarkDigested(digest);

        m.Status.Should().Be(MessageStatus.Digested);
        m.DigestId.Should().Be(digest);
        var again = () => m.MarkDigested(digest);
        again.Should().Throw<BusinessRuleViolationException>();
    }
}
