using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.SharedKernel.IntegrationEvents.AiMatching;

/// <summary>Published language of BC-10 AI Matching.</summary>
public abstract record AiMatchingIntegrationEvent(Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId, int Version = 1)
    : IntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId, Version)
{
    public override string Exchange => ExchangeNames.AiMatching;
    public override string Producer => BoundedContextSlugs.AiMatching;
}

public sealed record MatchScoreComputedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid MatchScoreId, Guid JobPostingId, Guid ProfileId, decimal Score, string ConfigVersion, long AggregateVersion)
    : AiMatchingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "MatchScoreComputed";
    public override string RoutingKey => RoutingKeys.MatchScoreComputed;
}

/// <summary>Carries the extracted, non-PII structured fields so BC-04 can merge them without a callback (gap G-15).</summary>
public sealed record ResumeParsedDataComputedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ResumeParsedDataId, Guid ResumeId, Guid ProfileId, string Language, string Status,
    IReadOnlyList<string> Skills, IReadOnlyList<string> JobTitles, int? YearsOfExperience, long AggregateVersion)
    : AiMatchingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ResumeParsedDataComputed";
    public override string RoutingKey => RoutingKeys.ResumeParsedDataComputed;
}

public sealed record SkillStandardizationUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid SkillStandardizationId, string FromStatus, string ToStatus, Guid ProfileId, string TaxonomyVersion, long AggregateVersion)
    : AiMatchingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "SkillStandardizationUpdated";
    public override string RoutingKey => RoutingKeys.SkillStandardizationUpdated;
}

public sealed record ParsedProfileDataUpdatedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid ParsedProfileDataId, Guid ResumeId, string FromStatus, string ToStatus, Guid ActorId, Guid ProfileId,
    IReadOnlyList<string> ChangedFields, IReadOnlyList<string> Skills, long AggregateVersion)
    : AiMatchingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "ParsedProfileDataUpdated";
    public override string RoutingKey => RoutingKeys.ParsedProfileDataUpdated;
}

public sealed record JobRecommendationComputedIntegrationEvent(
    Guid MessageId, DateTime OccurredOnUtc, Guid CorrelationId, Guid? CausationId,
    Guid JobRecommendationId, Guid ProfileId, Guid ActorId, IReadOnlyList<Guid> TopJobIds, string Strategy, long AggregateVersion)
    : AiMatchingIntegrationEvent(MessageId, OccurredOnUtc, CorrelationId, CausationId)
{
    public override string EventType => "JobRecommendationComputed";
    public override string RoutingKey => RoutingKeys.JobRecommendationComputed;
}
