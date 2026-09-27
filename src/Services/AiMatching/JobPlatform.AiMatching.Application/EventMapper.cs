using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.AiMatching.Application;

/// <summary>Maps BC-10 domain events to the five published integration events (handover 5.1). Payloads carry ids and non-PII facts only.</summary>
public sealed class AiMatchingEventMapper : IDomainEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent, DomainEventContext c) => domainEvent switch
    {
        MatchScoreComputedDomainEvent e => new MatchScoreComputedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.MatchScoreId, e.JobPostingId, e.ProfileId, e.Score, e.ConfigVersion.ToString(), c.AggregateVersion),

        ResumeParsedDataComputedDomainEvent e => new ResumeParsedDataComputedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.ResumeParsedDataId, e.ResumeId, e.ProfileId, e.Language.ToString(), e.Status.ToString(), e.Skills, e.JobTitles, e.YearsOfExperience, c.AggregateVersion),

        SkillStandardizationUpdatedDomainEvent e => new SkillStandardizationUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.SkillStandardizationId, e.FromStatus, e.ToStatus, e.ProfileId, e.TaxonomyVersion, c.AggregateVersion),

        ParsedProfileDataUpdatedDomainEvent e => new ParsedProfileDataUpdatedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.ParsedProfileDataId, e.ResumeId, e.FromStatus, e.ToStatus, e.ActorId, e.ProfileId, e.ChangedFields, e.Skills, c.AggregateVersion),

        JobRecommendationComputedDomainEvent e => new JobRecommendationComputedIntegrationEvent(Guid.NewGuid(), e.OccurredOnUtc, c.CorrelationId, c.CausationId,
            e.JobRecommendationId, e.ProfileId, e.ActorId, e.TopJobIds, e.Strategy.ToString(), c.AggregateVersion),

        _ => null // MatchingConfigurationChanged stays in-process (cache eviction)
    };
}
