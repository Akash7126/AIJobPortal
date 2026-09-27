using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;

/// <summary>Published language of BC-02 External Integration.</summary>
public abstract record ExternalIntegrationIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.ExternalIntegration;
    public override string Producer => BoundedContextSlugs.ExternalIntegration;
}

/// <summary>Enriched with the standardised job fields BC-09 needs to create or update a posting (BC-02 Q-05, gap G-13).</summary>
public sealed record JobDataImportedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobDataId, Guid SourcePlatformId, Guid ActorId, string PlatformJobId, string SourceJobId,
    string Title, string Summary, IReadOnlyList<string> Skills, string ContractType, string WorkFormat,
    DateTime? DeadlineUtc, string Location, string? SourceUrl, string AttributionVisibility, bool IsUpdate, long AggregateVersion)
    : ExternalIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobDataImported";
    public override string RoutingKey => RoutingKeys.JobDataImported;
}

/// <param name="ToStatus">Active, Closed, Deactivated, Deleted or Updated.</param>
public sealed record JobPostAttributionUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobPostAttributionId, string FromStatus, string ToStatus, Guid ActorId, string PlatformJobId,
    DateTime? DeadlineUtc, string? Description, long AggregateVersion)
    : ExternalIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobPostAttributionUpdated";
    public override string RoutingKey => RoutingKeys.JobPostAttributionUpdated;
}

public sealed record JobDataMappingUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobDataMappingId, string FromStatus, string ToStatus, Guid ActorId, Guid IntegrationId, int MappingVersion, long AggregateVersion)
    : ExternalIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobDataMappingUpdated";
    public override string RoutingKey => RoutingKeys.JobDataMappingUpdated;
}

public sealed record ExternalJobSiteIntegrationSupportedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ExternalJobSiteIntegrationId, Guid SourcePlatformId, IReadOnlyList<string> Models, long AggregateVersion)
    : ExternalIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ExternalJobSiteIntegrationSupported";
    public override string RoutingKey => RoutingKeys.ExternalJobSiteIntegrationSupported;
}

public sealed record AttributionVisibilityConfiguredIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid AttributionVisibilityId, Guid ActorId, Guid IntegrationId, string Visibility, long AggregateVersion)
    : ExternalIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "AttributionVisibilityConfigured";
    public override string RoutingKey => RoutingKeys.AttributionVisibilityConfigured;
}

public sealed record ApiSchemaDocumentationViewedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ApiSchemaDocumentationId, Guid ActorId, string ApiVersion, long AggregateVersion)
    : ExternalIntegrationIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ApiSchemaDocumentationViewed";
    public override string RoutingKey => RoutingKeys.ApiSchemaDocumentationViewed;
}
