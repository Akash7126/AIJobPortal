using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;

/// <summary>Published language of BC-03 Account Identity. Ids and non-PII fields only; no secrets, ever.</summary>
public abstract record AccountIdentityIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.AccountIdentity;
    public override string Producer => BoundedContextSlugs.AccountIdentity;
}

public sealed record AccountCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid AccountId, Guid ActorId, ActorType ActorType, long AggregateVersion)
    : AccountIdentityIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "AccountCreated";
    public override string RoutingKey => RoutingKeys.AccountCreated;
}

public sealed record AccountApprovedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid AccountId, Guid ActorId, ActorType ActorType, long AggregateVersion)
    : AccountIdentityIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "AccountApproved";
    public override string RoutingKey => RoutingKeys.AccountApproved;
}

/// <param name="Standing">"Deactivated" or "DeletionRequested".</param>
public sealed record AccountSuspendedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid AccountId, Guid ActorId, ActorType ActorType, string Reason, string Standing, long AggregateVersion)
    : AccountIdentityIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "AccountSuspended";
    public override string RoutingKey => RoutingKeys.AccountSuspended;
}

/// <summary>Never carries the key secret or its hash.</summary>
public sealed record ApiCredentialCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ApiCredentialId, Guid AccountId, Guid ActorId, DateTime ExpiresAtUtc, long AggregateVersion)
    : AccountIdentityIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ApiCredentialCreated";
    public override string RoutingKey => RoutingKeys.ApiCredentialCreated;
}

public sealed record UserAccountApprovedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid UserAccountId, Guid ActorId, string FromStanding, string ToStanding, long AggregateVersion)
    : AccountIdentityIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "UserAccountApproved";
    public override string RoutingKey => RoutingKeys.UserAccountApproved;
}
