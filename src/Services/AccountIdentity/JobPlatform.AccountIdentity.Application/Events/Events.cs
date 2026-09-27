using JobPlatform.AccountIdentity.Application.Abstractions;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Domain.ApiCredentials;
using JobPlatform.AccountIdentity.Domain.PasswordPolicies;
using JobPlatform.AccountIdentity.Domain.Rbac;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.AccountIdentity.Application.Events;

/// <summary>
/// Maps BC-03 domain events to the five published integration events (handover section 5.1). Payloads carry ids and non-PII fields only;
/// the API credential event never carries the secret or its hash.
/// </summary>
public sealed class AccountIdentityEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        AccountRegisteredDomainEvent e => new AccountCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.AccountId.Value, e.AccountId.Value, e.ActorType, c.AggregateVersion),

        AccountActivatedDomainEvent e => new AccountApprovedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.AccountId.Value, e.ActorId, e.ActorType, c.AggregateVersion),

        AccountSuspendedDomainEvent e => new AccountSuspendedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.AccountId.Value, e.ActorId, e.ActorType, e.Reason, e.Kind.ToString(), c.AggregateVersion),

        UserAccountStandingChangedDomainEvent e => new UserAccountApprovedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.AccountId.Value, e.ActorId, e.From.ToString(), e.To.ToString(), c.AggregateVersion),

        ApiCredentialIssuedDomainEvent e => new ApiCredentialCreatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.ApiCredentialId.Value, e.PartnerAccountId.Value, e.ActorId, e.ExpiresAtUtc, c.AggregateVersion),

        _ => null
    };
}

// ---------------------------------------------------------------------- in-process reactions (after commit)

internal sealed class RolePermissionsChangedHandler : IDomainEventHandler<RolePermissionsChangedDomainEvent>
{
    private readonly ICacheInvalidator _cache;

    public RolePermissionsChangedHandler(ICacheInvalidator cache) => _cache = cache;

    /// <summary>Role changes apply on the very next request: evict the role map immediately (US-3.1.5-03 AC-04).</summary>
    public Task Handle(RolePermissionsChangedDomainEvent domainEvent, CancellationToken ct) => _cache.InvalidateRoleMapAsync(ct);
}

internal sealed class AccountRolesChangedHandler : IDomainEventHandler<AccountRolesChangedDomainEvent>
{
    private readonly ICacheInvalidator _cache;

    public AccountRolesChangedHandler(ICacheInvalidator cache) => _cache = cache;

    public Task Handle(AccountRolesChangedDomainEvent domainEvent, CancellationToken ct) =>
        _cache.InvalidateAccountRolesAsync(domainEvent.AccountId.Value, ct);
}

internal sealed class PasswordPolicyConfiguredHandler : IDomainEventHandler<PasswordPolicyConfiguredDomainEvent>
{
    private readonly ICacheInvalidator _cache;

    public PasswordPolicyConfiguredHandler(ICacheInvalidator cache) => _cache = cache;

    public Task Handle(PasswordPolicyConfiguredDomainEvent domainEvent, CancellationToken ct) => _cache.InvalidatePasswordPolicyAsync(ct);
}
