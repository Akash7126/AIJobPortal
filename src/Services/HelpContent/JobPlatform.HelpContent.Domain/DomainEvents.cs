using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.HelpContent.Domain;

/// <summary>Published (mapped to NewsArticleCreatedIntegrationEvent). Raised only when a genuinely new draft is created (INV-02).</summary>
public sealed record NewsArticleCreatedDomainEvent(Guid NewsArticleId, Guid ActorId, string Kind, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to NewsArticlePublishedIntegrationEvent). CategoryIds are supplied by the caller (the application layer reads the
/// sibling ContentCategorization aggregate) rather than looked up by NewsArticle itself, which never depends on another aggregate.</summary>
public sealed record NewsArticlePublishedDomainEvent(Guid NewsArticleId, Guid ActorId, IReadOnlyList<Guid> CategoryIds, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to NewsArticleArchivedIntegrationEvent).</summary>
public sealed record NewsArticleArchivedDomainEvent(Guid NewsArticleId, Guid ActorId, DateTime At) : DomainEvent(At);

/// <summary>Published (mapped to HelpContentUpdatedIntegrationEvent). Q-02 (decided here): help content has no status, so FromStatus/ToStatus
/// in the frozen contract are populated with "Live" (help content is always live once created; only its version changes).</summary>
public sealed record HelpContentUpdatedDomainEvent(Guid HelpContentId, Guid ActorId, int FromVersion, int ToVersion, DateTime At) : DomainEvent(At);

/// <summary>Internal only (cache invalidation of the composed company page) - not mapped to an integration event; the handover catalogues no
/// event for company-page edits.</summary>
public sealed record CompanyPageChangedDomainEvent(Guid EmployerAccountId, DateTime At) : DomainEvent(At);
