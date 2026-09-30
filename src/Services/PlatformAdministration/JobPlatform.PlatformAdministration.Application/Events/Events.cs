using JobPlatform.PlatformAdministration.Application.Interfaces;
using JobPlatform.PlatformAdministration.Domain;
using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.PlatformAdministration.Domain.Offerings;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.PlatformAdministration.Application.Events;

/// <summary>Maps BC-08 domain events to the five published integration events (handover 5.1). Payloads carry ids and non-PII fields only; a setting value never travels.</summary>
public sealed class PlatformAdministrationEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        PlatformEntityRecordCreatedDomainEvent e => new PlatformEntityRecordCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.RecordId, e.ActorId, e.EntityType.ToString(), c.AggregateVersion),

        PlatformTaxonomyUpdatedDomainEvent e => new PlatformTaxonomyUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.TaxonomyId, $"v{e.FromVersion}", $"v{e.ToVersion}", e.ActorId, e.TaxonomyType, e.FromVersion, e.ToVersion, e.ChangedCodes, c.AggregateVersion),

        JobOfferingSuspendedDomainEvent e => new JobOfferingSuspendedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.JobOfferingId, e.JobOfferingId, e.ActorId, e.Reason, c.AggregateVersion),

        ReferenceFileUpdatedDomainEvent e => new ReferenceFileUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.FileId, e.Type.ToString().ToLowerInvariant(), e.FileVersion, e.ActorId, c.AggregateVersion),

        SystemSettingChangedDomainEvent e => new SystemSettingChangedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.SettingId, e.Key, e.SettingVersion, e.ActorId, c.AggregateVersion),

        _ => null
    };
}

/// <summary>
/// Inbox handler of JobPostingCreated (BC-09): the posting becomes eligible for oversight. Idempotent per jobPostingId, so a duplicate delivery
/// (a different message id for the same posting) and a redelivery are both harmless.
/// </summary>
public sealed class RegisterJobOfferingHandler : IIntegrationEventHandler<JobPostingCreatedIntegrationEvent>
{
    private readonly IJobOfferingRepository _offerings;
    private readonly TimeProvider _clock;

    public RegisterJobOfferingHandler(IJobOfferingRepository offerings, TimeProvider clock)
    {
        _offerings = offerings;
        _clock = clock;
    }

    public async Task Handle(JobPostingCreatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (await _offerings.ExistsAsync(integrationEvent.JobPostingId, ct))
        {
            return;
        }

        _offerings.Add(JobOffering.Register(integrationEvent.JobPostingId, integrationEvent.EmployerAccountId, integrationEvent.Title, _clock.GetUtcNow().UtcDateTime));
    }
}

// ---------------------------------------------------------------------- in-process reactions (after commit): cache invalidation (handover section 9)

internal sealed class TaxonomyUpdatedCacheHandler : IDomainEventHandler<PlatformTaxonomyUpdatedDomainEvent>
{
    private readonly IReferenceDataCache _cache;

    public TaxonomyUpdatedCacheHandler(IReferenceDataCache cache) => _cache = cache;

    /// <summary>Versioned content is immutable; only the "current" pointer moves.</summary>
    public Task Handle(PlatformTaxonomyUpdatedDomainEvent domainEvent, CancellationToken ct) =>
        _cache.RemoveAsync(CacheKeys.TaxonomyCurrent(domainEvent.TaxonomyType), ct);
}

internal sealed class ReferenceFileUpdatedCacheHandler : IDomainEventHandler<ReferenceFileUpdatedDomainEvent>
{
    private readonly IReferenceDataCache _cache;

    public ReferenceFileUpdatedCacheHandler(IReferenceDataCache cache) => _cache = cache;

    public Task Handle(ReferenceFileUpdatedDomainEvent domainEvent, CancellationToken ct) =>
        _cache.RemoveAsync(CacheKeys.Reference(domainEvent.Type.ToString().ToLowerInvariant()), ct);
}

internal sealed class SystemSettingChangedCacheHandler : IDomainEventHandler<SystemSettingChangedDomainEvent>
{
    private readonly IReferenceDataCache _cache;

    public SystemSettingChangedCacheHandler(IReferenceDataCache cache) => _cache = cache;

    public Task Handle(SystemSettingChangedDomainEvent domainEvent, CancellationToken ct) => _cache.RemoveAsync(CacheKeys.Setting(domainEvent.Key), ct);
}
