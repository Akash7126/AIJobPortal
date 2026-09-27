using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.GovernmentIntegration.Application;

/// <summary>Upserts the KnownAccounts replica (handover section 5.2, D-01): the local read model that satisfies EmployerVerification.Request's
/// precondition INV-02 without a synchronous call to BC-03 (foundation F-07). Idempotent and monotonic: a redelivery or an out-of-order
/// AccountCreated (lower or equal AggregateVersion) changes nothing.</summary>
public sealed class RecordKnownAccountHandler : IIntegrationEventHandler<AccountCreatedIntegrationEvent>
{
    private readonly IKnownAccountRepository _knownAccounts;

    public RecordKnownAccountHandler(IKnownAccountRepository knownAccounts) => _knownAccounts = knownAccounts;

    public async Task Handle(AccountCreatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        var existing = await _knownAccounts.GetAsync(integrationEvent.AccountId, ct);
        if (existing is not null)
        {
            // KnownAccount has no mutable fields worth updating (actor type never changes); a redelivery or an older/duplicate event is a no-op.
            return;
        }

        _knownAccounts.Add(new KnownAccount(integrationEvent.AccountId, integrationEvent.ActorType, integrationEvent.OccurredOnUtc,
            integrationEvent.AggregateVersion));
    }
}
