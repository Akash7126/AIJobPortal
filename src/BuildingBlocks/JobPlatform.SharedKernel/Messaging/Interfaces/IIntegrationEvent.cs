namespace JobPlatform.SharedKernel.Messaging.Interfaces;

public interface IIntegrationEvent
{
    Guid MessageId { get; }
    DateTime OccurredOnUtc { get; }
    Guid CorrelationId { get; }
    Guid? CausationId { get; }
    int Version { get; }

    /// <summary>Catalogue event name, e.g. AccountCreated.</summary>
    string EventType { get; }

    string Exchange { get; }

    string RoutingKey { get; }

    /// <summary>Slug of the publishing BC (message header "producer").</summary>
    string Producer { get; }
}
