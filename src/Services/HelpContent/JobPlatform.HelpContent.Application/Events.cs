using JobPlatform.HelpContent.Domain;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.HelpContent;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.HelpContent.Application.Events;

/// <summary>Maps BC-06 domain events to the four frozen published integration events (handover section 5.1). CompanyPageChangedDomainEvent
/// is internal-only (cache invalidation) and is not mapped.</summary>
public sealed class HelpContentEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        NewsArticleCreatedDomainEvent e => new NewsArticleCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.NewsArticleId, e.ActorId, e.Kind, c.AggregateVersion),

        NewsArticlePublishedDomainEvent e => new NewsArticlePublishedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.NewsArticleId, e.ActorId, e.CategoryIds, e.OccurredOnUtc, c.AggregateVersion),

        NewsArticleArchivedDomainEvent e => new NewsArticleArchivedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.NewsArticleId, e.ActorId, c.AggregateVersion),

        HelpContentUpdatedDomainEvent e => new HelpContentUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.HelpContentId, Domain.HelpContent.LiveStatus, Domain.HelpContent.LiveStatus, e.ActorId, e.FromVersion, e.ToVersion, c.AggregateVersion),

        _ => null
    };
}

// ---------------------------------------------------------------------- in-process reactions (after commit): cache invalidation (handover section 9)

internal sealed class CompanyPageChangedCacheHandler : IDomainEventHandler<CompanyPageChangedDomainEvent>
{
    private readonly IHelpContentCache _cache;

    public CompanyPageChangedCacheHandler(IHelpContentCache cache) => _cache = cache;

    public Task Handle(CompanyPageChangedDomainEvent domainEvent, CancellationToken ct) => _cache.InvalidateCompanyPageAsync(domainEvent.EmployerAccountId, ct);
}
