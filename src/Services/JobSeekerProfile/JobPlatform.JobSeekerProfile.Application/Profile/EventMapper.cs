using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.JobSeekerProfile.Application.Events;

/// <summary>Maps BC-04 domain events to the three published integration events (handover section 5.1).</summary>
public sealed class JobSeekerProfileEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        ProfileCreatedDomainEvent e => new ProfileCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.ProfileId, e.ActorId, e.OwnerAccountId, null, c.AggregateVersion),

        ProfileUpdatedDomainEvent e => new ProfileUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.ProfileId, e.FromStatus, e.ToStatus, e.ActorId, e.ChangedSections, e.CompletionPercent, c.AggregateVersion),

        ResumeCreatedDomainEvent e => new ResumeCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.ResumeId, e.ActorId, e.ProfileId, e.Format, e.SizeBytes, e.Sha256, c.AggregateVersion),

        _ => null
    };
}
