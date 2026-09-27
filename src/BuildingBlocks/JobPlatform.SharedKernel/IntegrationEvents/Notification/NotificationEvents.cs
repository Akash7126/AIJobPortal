using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.Notification;

/// <summary>Published language of BC-13 Notification. Recipient is masked; the message body is never published.</summary>
public abstract record NotificationIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.Notification;
    public override string Producer => BoundedContextSlugs.Notification;
}

/// <param name="Channel">Email, Sms or InApp.</param>
public sealed record NotificationSentIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid NotificationId, string Channel, string Category, Guid? RecipientAccountId, string MaskedRecipient, string? Subject, string Status, long AggregateVersion)
    : NotificationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "NotificationSent";
    public override string RoutingKey => RoutingKeys.NotificationSent;
}

public sealed record NotificationStatusUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid NotificationStatusId, string FromStatus, string ToStatus, Guid RecipientAccountId, long AggregateVersion)
    : NotificationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "NotificationStatusUpdated";
    public override string RoutingKey => RoutingKeys.NotificationStatusUpdated;
}

public sealed record JobConfirmationSentIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobConfirmationId, Guid ActorId, Guid SourcePlatformId, string PlatformJobId, long AggregateVersion)
    : NotificationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobConfirmationSent";
    public override string RoutingKey => RoutingKeys.JobConfirmationSent;
}
