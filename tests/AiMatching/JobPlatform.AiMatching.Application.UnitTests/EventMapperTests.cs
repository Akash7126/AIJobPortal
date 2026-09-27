using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.Messaging;

namespace JobPlatform.AiMatching.Application.UnitTests;

public class EventMapperTests
{
    private static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DomainEventContext Context = new("agg-1", 3, Guid.NewGuid(), Guid.NewGuid());
    private static readonly AiMatchingEventMapper Mapper = new();

    [Fact]
    public void Map_MatchScoreComputed_ProducesIntegrationEventWithSameFacts()
    {
        var domainEvent = new MatchScoreComputedDomainEvent(At, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 87m, 2);

        var mapped = Mapper.Map(domainEvent, Context).Should().BeOfType<MatchScoreComputedIntegrationEvent>().Which;

        mapped.MatchScoreId.Should().Be(domainEvent.MatchScoreId);
        mapped.JobPostingId.Should().Be(domainEvent.JobPostingId);
        mapped.ProfileId.Should().Be(domainEvent.ProfileId);
        mapped.Score.Should().Be(87m);
        mapped.AggregateVersion.Should().Be(3);
        mapped.CorrelationId.Should().Be(Context.CorrelationId);
    }

    [Fact]
    public void Map_ResumeParsedDataComputed_CarriesSkillsAndJobTitles()
    {
        var domainEvent = new ResumeParsedDataComputedDomainEvent(At, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TextLanguage.En, ParseStatus.Parsed,
            new[] { "C#" }, new[] { "Developer" }, 5);

        var mapped = Mapper.Map(domainEvent, Context).Should().BeOfType<ResumeParsedDataComputedIntegrationEvent>().Which;

        mapped.Skills.Should().Equal("C#");
        mapped.JobTitles.Should().Equal("Developer");
        mapped.YearsOfExperience.Should().Be(5);
        mapped.Language.Should().Be("En");
    }

    [Fact]
    public void Map_SkillStandardizationUpdated_CarriesTaxonomyVersion()
    {
        var domainEvent = new SkillStandardizationUpdatedDomainEvent(At, Guid.NewGuid(), Guid.NewGuid(), "None", "Standardized", "v2");

        var mapped = Mapper.Map(domainEvent, Context).Should().BeOfType<SkillStandardizationUpdatedIntegrationEvent>().Which;

        mapped.FromStatus.Should().Be("None");
        mapped.ToStatus.Should().Be("Standardized");
        mapped.TaxonomyVersion.Should().Be("v2");
    }

    [Fact]
    public void Map_ParsedProfileDataUpdated_CarriesActorAndChangedFields()
    {
        var actorId = Guid.NewGuid();
        var domainEvent = new ParsedProfileDataUpdatedDomainEvent(At, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Parsed", "Reviewed", actorId,
            new[] { "Skills" }, new[] { "SQL" });

        var mapped = Mapper.Map(domainEvent, Context).Should().BeOfType<ParsedProfileDataUpdatedIntegrationEvent>().Which;

        mapped.ActorId.Should().Be(actorId);
        mapped.ChangedFields.Should().Equal("Skills");
        mapped.Skills.Should().Equal("SQL");
    }

    [Fact]
    public void Map_JobRecommendationComputed_CarriesTopJobIdsAndStrategy()
    {
        var jobIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var domainEvent = new JobRecommendationComputedDomainEvent(At, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), jobIds, RecommendationStrategy.Hybrid);

        var mapped = Mapper.Map(domainEvent, Context).Should().BeOfType<JobRecommendationComputedIntegrationEvent>().Which;

        mapped.TopJobIds.Should().Equal(jobIds);
        mapped.Strategy.Should().Be("Hybrid");
    }

    [Fact]
    public void Map_MatchingConfigurationChanged_StaysInProcess_ReturnsNull()
    {
        var domainEvent = new MatchingConfigurationChangedDomainEvent(At, 2, "Threshold");

        Mapper.Map(domainEvent, Context).Should().BeNull();
    }
}
