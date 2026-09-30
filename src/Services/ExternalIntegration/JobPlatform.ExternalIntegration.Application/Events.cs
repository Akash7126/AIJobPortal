using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;

namespace JobPlatform.ExternalIntegration.Application.Events;

/// <summary>Maps BC-02 domain events to the six published integration events (handover 5.1). The frozen contracts live in
/// SharedKernel/IntegrationEvents/ExternalIntegration/ExternalIntegrationEvents.cs and are reproduced here exactly.</summary>
public sealed class ExternalIntegrationEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        JobDataImportedDomainEvent e => new JobDataImportedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.JobDataId, e.SourcePlatformId, e.ActorId, e.PlatformJobId, e.SourceJobId, e.Title, e.Summary, e.Skills, e.ContractType, e.WorkFormat,
            e.DeadlineUtc, e.Location, e.SourceUrl, e.AttributionVisibility, e.IsUpdate, c.AggregateVersion),

        JobPostAttributionUpdatedDomainEvent e => new JobPostAttributionUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId,
            c.CausationId, e.AttributionId, e.FromStatus, e.ToStatus, e.ActorId, e.PlatformJobId, e.DeadlineUtc, e.Description, c.AggregateVersion),

        JobDataMappingUpdatedDomainEvent e => new JobDataMappingUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId,
            c.CausationId, e.MappingId, e.FromStatus, e.ToStatus, e.ActorId, e.IntegrationId, e.Version, c.AggregateVersion),

        ExternalJobSiteIntegrationSupportedDomainEvent e => new ExternalJobSiteIntegrationSupportedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc,
            c.CorrelationId, c.CausationId, e.IntegrationId, e.SourcePlatformId, e.Models, c.AggregateVersion),

        AttributionVisibilityConfiguredDomainEvent e => new AttributionVisibilityConfiguredIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc,
            c.CorrelationId, c.CausationId, e.IntegrationId, e.ActorId, e.IntegrationId, e.Visibility, c.AggregateVersion),

        ApiSchemaDocumentationViewedDomainEvent e => new ApiSchemaDocumentationViewedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId,
            c.CausationId, e.ViewId, e.ActorId, e.ApiVersion, c.AggregateVersion),

        _ => null
    };
}

// ---------------------------------------------------------------------- inbox (handover 5.2)

/// <summary>Proposed consumer (handover 5.2 "RegisterPartnerAccountHandler"): records that a BC-03 account is a recognised, active
/// ExternalJobSite so partner self-registration (RegisterExternalJobSiteCommand) doesn't need a live call. Idempotent per account.</summary>
public sealed class RegisterKnownPartnerAccountHandler : IIntegrationEventHandler<AccountApprovedIntegrationEvent>
{
    private readonly IKnownPartnerAccountRepository _knownAccounts;
    private readonly TimeProvider _clock;

    public RegisterKnownPartnerAccountHandler(IKnownPartnerAccountRepository knownAccounts, TimeProvider clock)
    {
        _knownAccounts = knownAccounts;
        _clock = clock;
    }

    public async Task Handle(AccountApprovedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.ActorType != ActorType.ExternalJobSite)
        {
            return;
        }

        if (await _knownAccounts.GetAsync(integrationEvent.AccountId, ct) is null)
        {
            _knownAccounts.Add(new KnownPartnerAccount(integrationEvent.AccountId, _clock.GetUtcNow().UtcDateTime));
        }
    }
}

/// <summary>`RecordPartnerCredentialHandler` (handover 5.2): upserts the local PartnerCredentials replica by ApiCredentialId, ignoring an
/// older or equal aggregateVersion (idempotent/out-of-order safe).</summary>
public sealed class RecordPartnerCredentialHandler : IIntegrationEventHandler<ApiCredentialCreatedIntegrationEvent>
{
    private readonly IPartnerCredentialRepository _credentials;

    public RecordPartnerCredentialHandler(IPartnerCredentialRepository credentials) => _credentials = credentials;

    public async Task Handle(ApiCredentialCreatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        var existing = await _credentials.GetByIdAsync(integrationEvent.ApiCredentialId, ct);
        if (existing is null)
        {
            _credentials.Add(new PartnerCredential(integrationEvent.ApiCredentialId, integrationEvent.AccountId, integrationEvent.ExpiresAtUtc,
                integrationEvent.AggregateVersion));
        }
        else
        {
            existing.Update(integrationEvent.ExpiresAtUtc, integrationEvent.AggregateVersion);
        }
    }
}
