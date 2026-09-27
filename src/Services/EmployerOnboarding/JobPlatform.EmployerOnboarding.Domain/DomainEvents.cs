using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.EmployerOnboarding.Domain;

/// <summary>Published (mapped to EmployerRegistrationApprovedIntegrationEvent).</summary>
public sealed record EmployerRegistrationApprovedDomainEvent(Guid EmployerRegistrationId, Guid EmployerAccountId, Guid ActorId, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to CompanyMediaAndDocumentCreatedIntegrationEvent).</summary>
public sealed record CompanyMediaAndDocumentCreatedDomainEvent(Guid CompanyMediaId, Guid EmployerAccountId, Guid ActorId, string Kind, DateTime At) : DomainEvent(At);

/// <summary>Internal only (cache invalidation of the company public-info projection) - not mapped to an integration event.</summary>
public sealed record CompanyMediaChangedDomainEvent(Guid EmployerAccountId, DateTime At) : DomainEvent(At);

/// <summary>Internal only (cache invalidation of the standing projection) - not mapped to an integration event; handover publishes nothing on verification.</summary>
public sealed record EmployerStandingChangedDomainEvent(Guid EmployerAccountId, DateTime At) : DomainEvent(At);
