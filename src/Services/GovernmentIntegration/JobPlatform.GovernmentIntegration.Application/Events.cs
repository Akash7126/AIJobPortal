using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.GovernmentIntegration.Application.Events;

/// <summary>Maps BC-01's 6 domain events to the 6 frozen integration events (handover section 5.1). Every domain event this BC raises is
/// published - none are internal-only, since none of the aggregates need a further in-process reaction (no cache to invalidate here).</summary>
public sealed class GovernmentIntegrationEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        EmployerVerificationApprovedDomainEvent e => new EmployerVerificationApprovedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId,
            c.CausationId, e.EmployerVerificationId, e.EmployerAccountId, e.ActorId, e.Method, c.AggregateVersion),

        GovernmentVerificationDataImportedDomainEvent e => new GovernmentVerificationDataImportedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc,
            c.CorrelationId, c.CausationId, e.GovernmentVerificationDataId, e.SubjectType, e.SubjectId, e.Outcome, c.AggregateVersion),

        EducationalCredentialVerificationImportedDomainEvent e => new EducationalCredentialVerificationImportedIntegrationEvent(Guid.NewGuid(),
            e.OccurredOnUtc, c.CorrelationId, c.CausationId, e.EducationalCredentialVerificationId, e.SubjectId, e.Outcome, c.AggregateVersion),

        IdentityVerificationDataImportedDomainEvent e => new IdentityVerificationDataImportedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc,
            c.CorrelationId, c.CausationId, e.IdentityVerificationDataId, e.SubjectId, e.Outcome, c.AggregateVersion),

        LegacyDataImportedDomainEvent e => new LegacyDataImportedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.LegacyDataId, e.MigrationRunId, e.RecordType, e.SourceSystem, c.AggregateVersion),

        DataQualityUpdatedDomainEvent e => new DataQualityUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.DataQualityId, e.FromStatus, e.ToStatus, e.MigrationRunId, e.RecordsChecked, e.RecordsRejected, c.AggregateVersion),

        _ => null
    };
}
