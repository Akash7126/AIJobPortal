using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.HelpContent;

/// <summary>Published language of BC-06 Help Content.</summary>
public abstract record HelpContentIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.HelpContent;
    public override string Producer => BoundedContextSlugs.HelpContent;
}

public sealed record NewsArticleCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid NewsArticleId, Guid ActorId, string Kind, long AggregateVersion)
    : HelpContentIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "NewsArticleCreated";
    public override string RoutingKey => RoutingKeys.NewsArticleCreated;
}

public sealed record NewsArticlePublishedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid NewsArticleId, Guid ActorId, IReadOnlyList<Guid> CategoryIds, DateTime PublishedAtUtc, long AggregateVersion)
    : HelpContentIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "NewsArticlePublished";
    public override string RoutingKey => RoutingKeys.NewsArticlePublished;
}

public sealed record NewsArticleArchivedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid NewsArticleId, Guid ActorId, long AggregateVersion)
    : HelpContentIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "NewsArticleArchived";
    public override string RoutingKey => RoutingKeys.NewsArticleArchived;
}

public sealed record HelpContentUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid HelpContentId, string FromStatus, string ToStatus, Guid ActorId, int FromVersion, int ToVersion, long AggregateVersion)
    : HelpContentIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "HelpContentUpdated";
    public override string RoutingKey => RoutingKeys.HelpContentUpdated;
}
