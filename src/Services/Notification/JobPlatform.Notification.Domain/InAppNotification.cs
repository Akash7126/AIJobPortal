using JobPlatform.SharedKernel.Common.ValueObjects;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.Notification.Domain;

public sealed record InAppNotificationCreatedDomainEvent(DateTime At, Guid NotificationId, Guid RecipientId, string TypeCode, string Category)
    : DomainEvent(At);

public sealed record InAppNotificationStatusChangedDomainEvent(DateTime At, Guid NotificationId, Guid RecipientId, InAppStatus From, InAppStatus To)
    : DomainEvent(At);

/// <summary>
/// In-app notification carrying the catalogued NotificationStatus (AGG-29, decision D-01): content and unread/read/deleted status in one aggregate.
/// Only the recipient may change it (INV-02); a deleted notification is gone for everyone (INV-03).
/// </summary>
public sealed class InAppNotification : AggregateRoot<Guid>
{
    private InAppNotification()
    {
    }

    public Guid RecipientAccountId { get; private set; }
    public string TypeCode { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string TitleAr { get; private set; } = string.Empty;
    public string TitleEn { get; private set; } = string.Empty;
    public string BodyAr { get; private set; } = string.Empty;
    public string BodyEn { get; private set; } = string.Empty;
    public string? ActionUrl { get; private set; }
    public InAppStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public bool DeliveredRealtime { get; private set; }

    public LocalizedText Title => new(TitleAr, TitleEn);
    public LocalizedText Body => new(BodyAr, BodyEn);

    public static InAppNotification Create(Guid recipient, string typeCode, string category, LocalizedText title, LocalizedText body, string? actionUrl, DateTime nowUtc)
    {
        Guard.Ensure(recipient != Guid.Empty, NotificationRuleCodes.InvalidMessage, "A notification needs a recipient.");
        Guard.Ensure(!string.IsNullOrWhiteSpace(typeCode), NotificationRuleCodes.InvalidMessage, "A notification needs a type code.");
        Guard.Ensure(!string.IsNullOrWhiteSpace(title.En) || !string.IsNullOrWhiteSpace(title.Ar), NotificationRuleCodes.InvalidMessage, "A notification needs a title.");
        var notification = new InAppNotification
        {
            Id = Guid.NewGuid(),
            RecipientAccountId = recipient,
            TypeCode = typeCode,
            Category = category,
            TitleAr = title.Ar,
            TitleEn = title.En,
            BodyAr = body.Ar,
            BodyEn = body.En,
            ActionUrl = actionUrl,
            Status = InAppStatus.Unread,
            CreatedAtUtc = nowUtc
        };
        notification.Raise(new InAppNotificationCreatedDomainEvent(nowUtc, notification.Id, recipient, typeCode, category));
        return notification;
    }

    /// <summary>Recorded when the real-time push reached a connected recipient; an offline user just keeps it Unread.</summary>
    public void MarkDeliveredRealtime() => DeliveredRealtime = true;

    public void MarkRead(Actor actor, DateTime nowUtc)
    {
        EnsureRecipient(actor);
        EnsureNotDeleted();
        if (Status == InAppStatus.Read)
        {
            return;
        }

        Transition(InAppStatus.Read, nowUtc);
        ReadAtUtc = nowUtc;
    }

    public void Delete(Actor actor, DateTime nowUtc)
    {
        EnsureRecipient(actor);
        EnsureNotDeleted();
        Transition(InAppStatus.Deleted, nowUtc);
    }

    /// <summary>Marks the notification read and returns its action link (3.6.2-07 AC-02).</summary>
    public string? TakeAction(Actor actor, DateTime nowUtc)
    {
        MarkRead(actor, nowUtc);
        return ActionUrl;
    }

    private void Transition(InAppStatus to, DateTime nowUtc)
    {
        var from = Status;
        Status = to;
        Raise(new InAppNotificationStatusChangedDomainEvent(nowUtc, Id, RecipientAccountId, from, to));
    }

    private void EnsureRecipient(Actor actor) =>
        Guard.Ensure(actor.Id == RecipientAccountId, NotificationRuleCodes.NotRecipient, "Only the recipient may change this notification.",
            NotificationErrorCodes.InAppForbidden, BusinessRuleKind.Forbidden);

    private void EnsureNotDeleted() =>
        Guard.Ensure(Status != InAppStatus.Deleted, NotificationRuleCodes.AlreadyDeleted, "The notification was deleted.", NotificationErrorCodes.InAppNotFound,
            BusinessRuleKind.Conflict);
}
