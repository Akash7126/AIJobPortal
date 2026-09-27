using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.ExternalIntegration.Domain;

/// <summary>Published (mapped to ExternalJobSiteIntegrationSupportedIntegrationEvent).</summary>
public sealed record ExternalJobSiteIntegrationSupportedDomainEvent(
    Guid IntegrationId, Guid SourcePlatformId, IReadOnlyList<string> Models, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to AttributionVisibilityConfiguredIntegrationEvent).</summary>
public sealed record AttributionVisibilityConfiguredDomainEvent(
    Guid IntegrationId, Guid ActorId, string Visibility, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to JobDataImportedIntegrationEvent) — BC-02 Q-05: enriched with the standardised fields BC-09 needs.</summary>
public sealed record JobDataImportedDomainEvent(
    Guid JobDataId, Guid SourcePlatformId, Guid ActorId, string PlatformJobId, string SourceJobId,
    string Title, string Summary, IReadOnlyList<string> Skills, string ContractType, string WorkFormat,
    DateTime? DeadlineUtc, string Location, string? SourceUrl, string AttributionVisibility, bool IsUpdate, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to JobPostAttributionUpdatedIntegrationEvent). ToStatus is Active, Closed, Deactivated, Deleted or Updated.</summary>
public sealed record JobPostAttributionUpdatedDomainEvent(
    Guid AttributionId, string FromStatus, string ToStatus, Guid ActorId, string PlatformJobId,
    DateTime? DeadlineUtc, string? Description, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to JobDataMappingUpdatedIntegrationEvent) on every saved change (handover 3.4).</summary>
public sealed record JobDataMappingUpdatedDomainEvent(
    Guid MappingId, string FromStatus, string ToStatus, Guid ActorId, Guid IntegrationId, int Version, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to ApiSchemaDocumentationViewedIntegrationEvent) — handover Q-03: published on every view.</summary>
public sealed record ApiSchemaDocumentationViewedDomainEvent(
    Guid ViewId, Guid ActorId, string ApiVersion, DateTime At) : DomainEvent(At);
