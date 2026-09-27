using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;

/// <summary>Published language of BC-08 Platform Administration (reference-data authority).</summary>
public abstract record PlatformAdministrationIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.PlatformAdministration;
    public override string Producer => BoundedContextSlugs.PlatformAdministration;
}

public sealed record PlatformEntityRecordCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid PlatformEntityRecordId, Guid ActorId, string EntityType, long AggregateVersion)
    : PlatformAdministrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "PlatformEntityRecordCreated";
    public override string RoutingKey => RoutingKeys.PlatformEntityRecordCreated;
}

public sealed record PlatformTaxonomyUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid PlatformTaxonomyId, string FromStatus, string ToStatus, Guid ActorId, string TaxonomyType, int FromVersion, int ToVersion,
    IReadOnlyList<string> ChangedCodes, long AggregateVersion)
    : PlatformAdministrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "PlatformTaxonomyUpdated";
    public override string RoutingKey => RoutingKeys.PlatformTaxonomyUpdated;
}

public sealed record JobOfferingSuspendedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobOfferingId, Guid JobPostingId, Guid ActorId, string Reason, long AggregateVersion)
    : PlatformAdministrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobOfferingSuspended";
    public override string RoutingKey => RoutingKeys.JobOfferingSuspended;
}

public sealed record ReferenceFileUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ReferenceFileId, string Type, int FileVersion, Guid ActorId, long AggregateVersion)
    : PlatformAdministrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ReferenceFileUpdated";
    public override string RoutingKey => RoutingKeys.ReferenceFileUpdated;
}

public sealed record SystemSettingChangedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid SystemSettingId, string Key, int SettingVersion, Guid ActorId, long AggregateVersion)
    : PlatformAdministrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "SystemSettingChanged";
    public override string RoutingKey => RoutingKeys.SystemSettingChanged;
}
