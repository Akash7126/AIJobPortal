using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;

/// <summary>Published language of BC-05 Employer Onboarding.</summary>
public abstract record EmployerOnboardingIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.EmployerOnboarding;
    public override string Producer => BoundedContextSlugs.EmployerOnboarding;
}

public sealed record EmployerRegistrationApprovedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid EmployerRegistrationId, Guid ActorId, Guid EmployerAccountId, long AggregateVersion)
    : EmployerOnboardingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "EmployerRegistrationApproved";
    public override string RoutingKey => RoutingKeys.EmployerRegistrationApproved;
}

public sealed record CompanyMediaAndDocumentCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid CompanyMediaAndDocumentId, Guid ActorId, Guid EmployerAccountId, string Kind, long AggregateVersion)
    : EmployerOnboardingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "CompanyMediaAndDocumentCreated";
    public override string RoutingKey => RoutingKeys.CompanyMediaAndDocumentCreated;
}
