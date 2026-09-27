using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;

/// <summary>Published language of BC-11 Candidate Sourcing. Never carries candidate PII.</summary>
public abstract record CandidateSourcingIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.CandidateSourcing;
    public override string Producer => BoundedContextSlugs.CandidateSourcing;
}

public sealed record TalentPoolEntryCreatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid TalentPoolEntryId, Guid JobPostingId, Guid ActorId, Guid EmployerAccountId, long AggregateVersion)
    : CandidateSourcingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "TalentPoolEntryCreated";
    public override string RoutingKey => RoutingKeys.TalentPoolEntryCreated;
}

public sealed record CandidateInsightComputedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid CandidateInsightId, Guid JobPostingId, Guid ActorId, Guid EmployerAccountId, Guid CandidateProfileId,
    string? Availability, decimal? ExpectedSalary, decimal? FitScore, IReadOnlyList<string> WithheldFields, long AggregateVersion)
    : CandidateSourcingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "CandidateInsightComputed";
    public override string RoutingKey => RoutingKeys.CandidateInsightComputed;
}
