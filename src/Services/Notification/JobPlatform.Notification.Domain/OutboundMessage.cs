using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain;

public sealed record OutboundMessageSentDomainEvent(DateTime At, Guid MessageId, Channel Channel, string Category, Guid RecipientId, string MaskedRecipient, string? Subject)
    : DomainEvent(At);

/// <summary>
/// E-mail or SMS to one recipient (proposed aggregate). Composed idempotently by dedupe key (INV-06), delivered with a bounded retry policy
/// (30 s timeout, 3 retries, then the channel's upstream-timeout code) and finally confirmed by the provider's delivery report, which stays
/// Pending until reported and is never assumed (3.6.3-04 AC-02). The recipient's address is never stored: it is resolved at send time.
/// </summary>
public sealed class OutboundMessage : AggregateRoot<Guid>
{
    public const int MaxRetries = 3;

    private OutboundMessage()
    {
    }

    public Channel Channel { get; private set; }
    public Guid RecipientAccountId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string DedupeKey { get; private set; } = string.Empty;
    public string? TemplateCode { get; private set; }
    public int? TemplateVersion { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string? Body { get; private set; }
    public string Locale { get; private set; } = "en";
    public MessageStatus Status { get; private set; }
    public string? SuppressionReason { get; private set; }
    public int Attempts { get; private set; }
    public string? ErrorCode { get; private set; }
    public DeliveryStatus DeliveryStatus { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? SenderIdentity { get; private set; }
    public Guid? DigestId { get; private set; }
    public bool IsDigestCandidate { get; private set; }
    public bool ContainsSecret { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime NextAttemptUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    /// <summary>Composes a message ready to send (Pending).</summary>
    public static OutboundMessage Compose(Channel channel, Guid recipient, string category, string dedupeKey, string subject, string body, string locale,
        string? templateCode, int? templateVersion, bool digestCandidate, bool containsSecret, DateTime nowUtc)
    {
        Guard.Ensure(channel != Channel.InApp, NotificationRuleCodes.InvalidMessage, "Outbound messages are e-mail or SMS.");
        Guard.Ensure(recipient != Guid.Empty && !string.IsNullOrWhiteSpace(dedupeKey) && !string.IsNullOrWhiteSpace(body), NotificationRuleCodes.InvalidMessage,
            "A message needs a recipient, a dedupe key and a body.");
        return new OutboundMessage
        {
            Id = Guid.NewGuid(),
            Channel = channel,
            RecipientAccountId = recipient,
            Category = category,
            DedupeKey = dedupeKey,
            TemplateCode = templateCode,
            TemplateVersion = templateVersion,
            Subject = subject,
            Body = body,
            Locale = locale,
            Status = MessageStatus.Pending,
            DeliveryStatus = DeliveryStatus.Pending,
            IsDigestCandidate = digestCandidate,
            ContainsSecret = containsSecret,
            CreatedAtUtc = nowUtc,
            NextAttemptUtc = nowUtc
        };
    }

    /// <summary>A message the preferences, opt-out or compliance rules refused: kept with the reason, never sent.</summary>
    public static OutboundMessage Suppressed(Channel channel, Guid recipient, string category, string dedupeKey, string reason, DateTime nowUtc)
    {
        var message = Compose(channel, recipient, category, dedupeKey, string.Empty, "-", "en", null, null, false, false, nowUtc);
        message.Body = null;
        message.Status = MessageStatus.Suppressed;
        message.SuppressionReason = reason;
        return message;
    }

    public bool IsDue(DateTime nowUtc) => Status == MessageStatus.Pending && !IsDigestCandidate && NextAttemptUtc <= nowUtc;

    public void BeginSending()
    {
        Guard.Ensure(Status == MessageStatus.Pending, NotificationRuleCodes.InvalidTransition, "Only a pending message can start sending.", null, BusinessRuleKind.Conflict);
        Status = MessageStatus.Sending;
        Attempts++;
    }

    public void MarkSent(string providerMessageId, string maskedRecipient, string? senderIdentity, DateTime nowUtc)
    {
        Guard.Ensure(Status == MessageStatus.Sending, NotificationRuleCodes.InvalidTransition, "Only a message being sent can be marked sent.", null, BusinessRuleKind.Conflict);
        Status = MessageStatus.Sent;
        ProviderMessageId = providerMessageId;
        SenderIdentity = senderIdentity;
        SentAtUtc = nowUtc;
        ErrorCode = null;
        DeliveryStatus = DeliveryStatus.Pending; // delivered only when the provider reports it
        ClearSecret();
        Raise(new OutboundMessageSentDomainEvent(nowUtc, Id, Channel, Category, RecipientAccountId, maskedRecipient, Channel == Channel.Email ? Subject : null));
    }

    /// <summary>Records a failed attempt: back to Pending with a backoff while retries remain, then Failed with the channel's upstream-timeout code.</summary>
    public void RecordFailure(string errorCode, DateTime nowUtc)
    {
        Guard.Ensure(Status == MessageStatus.Sending, NotificationRuleCodes.InvalidTransition, "Only a message being sent can fail.", null, BusinessRuleKind.Conflict);
        if (Attempts > MaxRetries)
        {
            Status = MessageStatus.Failed;
            ErrorCode = errorCode;
            DeliveryStatus = DeliveryStatus.Failed;
            ClearSecret();
            return;
        }

        Status = MessageStatus.Pending;
        ErrorCode = errorCode;
        NextAttemptUtc = nowUtc + TimeSpan.FromSeconds(30 * Math.Pow(2, Attempts - 1));
    }

    public static string TimeoutCode(Channel channel) =>
        channel == Channel.Sms ? NotificationErrorCodes.SmsUpstreamTimeout : NotificationErrorCodes.EmailUpstreamTimeout;

    /// <summary>Provider callback. Ignored until the message was actually sent; a final report is not overwritten by a later contradictory one.</summary>
    public bool RecordDeliveryReport(DeliveryStatus status)
    {
        if (Status != MessageStatus.Sent || status == DeliveryStatus.Pending)
        {
            return false;
        }

        if (DeliveryStatus != DeliveryStatus.Pending)
        {
            return false;
        }

        DeliveryStatus = status;
        return true;
    }

    /// <summary>Digest handling: the original message is replaced by the digest that carries it.</summary>
    public void MarkDigested(Guid digestId)
    {
        Guard.Ensure(Status == MessageStatus.Pending && IsDigestCandidate, NotificationRuleCodes.InvalidTransition, "Only a pending digest candidate can be digested.", null,
            BusinessRuleKind.Conflict);
        Status = MessageStatus.Digested;
        DigestId = digestId;
        Body = null;
    }

    private void ClearSecret()
    {
        if (ContainsSecret)
        {
            Body = null; // one-time codes must not stay in the database once the send is over
        }
    }
}
