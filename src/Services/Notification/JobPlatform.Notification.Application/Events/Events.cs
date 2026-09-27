using JobPlatform.Notification.Domain;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.Notification.Application.Events;

/// <summary>Maps BC-13 domain events to the three published integration events (handover 5.1). Recipients are masked and bodies never travel.</summary>
public sealed class NotificationEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        InAppNotificationCreatedDomainEvent e => new NotificationSentIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.NotificationId, "InApp", e.Category, e.RecipientId, "in-app", null, "Unread", c.AggregateVersion),

        InAppNotificationStatusChangedDomainEvent e => new NotificationStatusUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.NotificationId, e.From.ToString(), e.To.ToString(), e.RecipientId, c.AggregateVersion),

        OutboundMessageSentDomainEvent e => new NotificationSentIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.MessageId, e.Channel.ToString(), e.Category, e.RecipientId, e.MaskedRecipient, e.Subject, "Sent", c.AggregateVersion),

        JobConfirmationSentDomainEvent e => new JobConfirmationSentIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.JobConfirmationId, e.PartnerAccountId, e.SourcePlatformId, e.PlatformJobId, c.AggregateVersion),

        _ => null
    };
}
