using System.Reflection;
using JobPlatform.PlatformAdministration.Application;
using JobPlatform.PlatformAdministration.Domain.Entities;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;

namespace JobPlatform.PlatformAdministration.ArchitectureTests;

/// <summary>Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row) for BC-08.</summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(PlatformEntityRecord).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(AdminDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string ApplicationNs = "JobPlatform.PlatformAdministration.Application";
    private const string InfrastructureNs = "JobPlatform.PlatformAdministration.Infrastructure";
    private const string ApiNs = "JobPlatform.PlatformAdministration.Api";

    [Fact]
    public void Domain_DependsOnSharedKernelOnly_AndHasNoFrameworkOrHttp() =>
        ArchitectureRules.DomainDependsOn(Domain, ApplicationNs, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "System.Net.Http", "Serilog", "Microsoft.Extensions.Logging")
            .Should().BeEmpty();

    [Fact]
    public void Application_DependsOnDomainAndSharedKernelOnly() =>
        ArchitectureRules.DoesNotDependOn(Application, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore", "RabbitMQ", "StackExchange.Redis", "System.Net.Http").Should().BeEmpty();

    [Fact]
    public void Infrastructure_DoesNotDependOnTheApiHost() =>
        ArchitectureRules.DoesNotDependOn(Infrastructure, ApiNs).Should().BeEmpty();

    [Fact]
    public void Api_NeverReferencesAnotherBoundedContext() =>
        ArchitectureRules.ReferencedOtherBoundedContexts("PlatformAdministration", Domain, Application, Infrastructure, Api).Should().BeEmpty();

    [Fact]
    public void CommandsQueriesAndHandlers_FollowTheFeatureFolders_OneHandlerPerRequest() =>
        ArchitectureRules.CqrsLayoutViolations(Application).Should().BeEmpty();

    [Fact]
    public void Validators_LiveInTheValidatorsFolder() => ArchitectureRules.ValidatorsOutsideValidatorsFolder(Application).Should().BeEmpty();

    [Fact]
    public void Handlers_AreInternalAndSealed() => ArchitectureRules.HandlersNotInternalSealed(Application).Should().BeEmpty();

    [Fact]
    public void AggregatesAndEntities_HaveNoPublicSetters() => ArchitectureRules.PublicSettersInDomain(Domain).Should().BeEmpty();

    [Fact]
    public void Controllers_HoldNoDbContext() => ArchitectureRules.ControllersUsingPersistence(Api).Should().BeEmpty();

    [Fact]
    public void AggregateRoots_AreSealed()
    {
        var roots = Domain.GetTypes().Where(t => InheritsFromGeneric(t, typeof(AggregateRoot<>))).ToList();

        roots.Select(r => r.Name).Should().BeEquivalentTo("PlatformEntityRecord", "JobOffering", "ReferenceFile", "SystemSetting", "PlatformTaxonomy");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    [Fact]
    public void Validators_AreNamedValidator()
    {
        var validators = Application.GetTypes().Where(t => t.BaseType is { IsGenericType: true } bt
            && bt.GetGenericTypeDefinition().Name.StartsWith("AbstractValidator")).ToList();

        validators.Should().NotBeEmpty();
        validators.Should().OnlyContain(v => v.Name.EndsWith("Validator"));
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndIntegrationEventsLiveInTheSharedKernel()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var integrationEvents = SharedKernel.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
            && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration").ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        integrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        integrationEvents.Should().HaveCount(5, "BC-08 publishes PlatformEntityRecordCreated, PlatformTaxonomyUpdated, JobOfferingSuspended, " +
            "ReferenceFileUpdated and SystemSettingChanged");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract");
    }

    [Fact]
    public void IntegrationEventContracts_MatchTheFrozenSnapshot()
    {
        var recordCreated = new PlatformEntityRecordCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), "Skill", 1);
        var taxonomyUpdated = new PlatformTaxonomyUpdatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "Draft",
            "Published", Guid.NewGuid(), "Skills", 1, 2, new[] { "csharp" }, 1);
        var offeringSuspended = new JobOfferingSuspendedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "Policy violation", 1);
        var referenceFileUpdated = new ReferenceFileUpdatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(),
            "Governorates", 1, Guid.NewGuid(), 1);
        var settingChanged = new SystemSettingChangedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "Match.Threshold",
            1, Guid.NewGuid(), 1);
        var common = new[] { "messageId", "occurredOnUtc", "correlationId", "causationId", "version" };

        JsonProperties(recordCreated).Should().BeEquivalentTo(common.Concat(new[] { "platformEntityRecordId", "actorId", "entityType", "aggregateVersion" }));
        JsonProperties(taxonomyUpdated).Should().BeEquivalentTo(common.Concat(new[]
            { "platformTaxonomyId", "fromStatus", "toStatus", "actorId", "taxonomyType", "fromVersion", "toVersion", "changedCodes", "aggregateVersion" }));
        JsonProperties(offeringSuspended).Should().BeEquivalentTo(common.Concat(new[]
            { "jobOfferingId", "jobPostingId", "actorId", "reason", "aggregateVersion" }));
        JsonProperties(referenceFileUpdated).Should().BeEquivalentTo(common.Concat(new[] { "referenceFileId", "type", "fileVersion", "actorId", "aggregateVersion" }));
        JsonProperties(settingChanged).Should().BeEquivalentTo(common.Concat(new[] { "systemSettingId", "key", "settingVersion", "actorId", "aggregateVersion" }));
    }

    private static IEnumerable<string> JsonProperties(IIntegrationEvent e) =>
        System.Text.Json.Nodes.JsonNode.Parse(IntegrationJson.Serialize(e))!.AsObject().Select(p => p.Key);

    private static bool InheritsFromGeneric(Type type, Type genericBase)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == genericBase)
            {
                return true;
            }
        }

        return false;
    }
}
