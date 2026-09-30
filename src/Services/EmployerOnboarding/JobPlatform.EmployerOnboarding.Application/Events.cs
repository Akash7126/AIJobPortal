using JobPlatform.EmployerOnboarding.Application.Interfaces;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.EmployerOnboarding.Application.Events;

/// <summary>Maps BC-05 domain events to the two published integration events (handover 5.1). CompanyMediaChangedDomainEvent and
/// EmployerStandingChangedDomainEvent are internal-only (cache invalidation) and are not mapped.</summary>
public sealed class EmployerOnboardingEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        EmployerRegistrationApprovedDomainEvent e => new EmployerRegistrationApprovedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId,
            c.CausationId, e.EmployerRegistrationId, e.ActorId, e.EmployerAccountId, c.AggregateVersion),

        CompanyMediaAndDocumentCreatedDomainEvent e => new CompanyMediaAndDocumentCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId,
            c.CausationId, e.CompanyMediaId, e.ActorId, e.EmployerAccountId, e.Kind, c.AggregateVersion),

        _ => null
    };
}

// ---------------------------------------------------------------------- in-process reactions (after commit): cache invalidation (handover section 9)

internal sealed class CompanyMediaChangedCacheHandler : IDomainEventHandler<CompanyMediaChangedDomainEvent>
{
    private readonly IEmployerCache _cache;

    public CompanyMediaChangedCacheHandler(IEmployerCache cache) => _cache = cache;

    public Task Handle(CompanyMediaChangedDomainEvent domainEvent, CancellationToken ct) => _cache.InvalidateAsync(domainEvent.EmployerAccountId, ct);
}

internal sealed class EmployerStandingChangedCacheHandler : IDomainEventHandler<EmployerStandingChangedDomainEvent>
{
    private readonly IEmployerCache _cache;

    public EmployerStandingChangedCacheHandler(IEmployerCache cache) => _cache = cache;

    public Task Handle(EmployerStandingChangedDomainEvent domainEvent, CancellationToken ct) => _cache.InvalidateAsync(domainEvent.EmployerAccountId, ct);
}
