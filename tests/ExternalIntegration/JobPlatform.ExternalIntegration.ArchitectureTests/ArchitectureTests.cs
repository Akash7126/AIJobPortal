using System.Reflection;
using JobPlatform.ExternalIntegration.Application;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;

namespace JobPlatform.ExternalIntegration.ArchitectureTests;

/// <summary>Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row).</summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(ExternalJobSiteIntegration).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(ExternalIntegrationDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string ApplicationNs = "JobPlatform.ExternalIntegration.Application";
    private const string InfrastructureNs = "JobPlatform.ExternalIntegration.Infrastructure";
    private const string ApiNs = "JobPlatform.ExternalIntegration.Api";

    [Fact]
    public void Domain_DependsOnSharedKernelOnly_AndHasNoFrameworkOrHttp() =>
        ArchitectureRules.DomainDependsOn(Domain, ApplicationNs, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "System.Net.Http",
            "Serilog", "Microsoft.Extensions.Logging").Should().BeEmpty();

    [Fact]
    public void Application_DependsOnDomainAndSharedKernelOnly() =>
        ArchitectureRules.DoesNotDependOn(Application, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore", "RabbitMQ", "StackExchange.Redis", "System.Net.Http").Should().BeEmpty();

    [Fact]
    public void Infrastructure_DoesNotDependOnTheApiHost() => ArchitectureRules.DoesNotDependOn(Infrastructure, ApiNs).Should().BeEmpty();

    [Fact]
    public void Api_NeverReferencesAnotherBoundedContext() =>
        ArchitectureRules.ReferencedOtherBoundedContexts("ExternalIntegration", Domain, Application, Infrastructure, Api).Should().BeEmpty();

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

        roots.Select(r => r.Name).Should().BeEquivalentTo("ExternalJobSiteIntegration", "JobData", "JobPostAttribution", "JobDataMapping",
            "ApiVersion", "SoftwareInterfaceConnection", "ApiSchemaAccessLog");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndIntegrationEventsLiveInTheSharedKernel()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var integrationEvents = SharedKernel.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
            && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration").ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        integrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        integrationEvents.Should().HaveCount(6, "BC-02 publishes exactly six events (handover 5.1)");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract");
    }

    [Fact]
    public void IntegrationEventContracts_MatchTheFrozenSnapshot()
    {
        var now = DateTime.UtcNow;
        var imported = new JobDataImportedIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "p1", "s1", "T", "S", new[] { "C#" }, "FullTime", "Remote", null, "Ramallah", null, "Public", false, 1);
        var attribution = new JobPostAttributionUpdatedIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, Guid.NewGuid(), "Active", "Closed",
            Guid.NewGuid(), "p1", null, null, 1);
        var mapping = new JobDataMappingUpdatedIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, Guid.NewGuid(), "Configured", "Configured",
            Guid.NewGuid(), Guid.NewGuid(), 1, 1);
        var supported = new ExternalJobSiteIntegrationSupportedIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            new[] { "pull" }, 1);
        var visibility = new AttributionVisibilityConfiguredIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "Public", 1);
        var viewed = new ApiSchemaDocumentationViewedIntegrationEvent(Guid.NewGuid(), now, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "v1", 1);

        var common = new[] { "messageId", "occurredOnUtc", "correlationId", "causationId", "version" };
        JsonProperties(imported).Should().BeEquivalentTo(common.Concat(new[]
        {
            "jobDataId", "sourcePlatformId", "actorId", "platformJobId", "sourceJobId", "title", "summary", "skills", "contractType", "workFormat",
            "deadlineUtc", "location", "sourceUrl", "attributionVisibility", "isUpdate", "aggregateVersion"
        }));
        JsonProperties(attribution).Should().BeEquivalentTo(common.Concat(new[]
        {
            "jobPostAttributionId", "fromStatus", "toStatus", "actorId", "platformJobId", "deadlineUtc", "description", "aggregateVersion"
        }));
        JsonProperties(mapping).Should().BeEquivalentTo(common.Concat(new[]
        {
            "jobDataMappingId", "fromStatus", "toStatus", "actorId", "integrationId", "mappingVersion", "aggregateVersion"
        }));
        JsonProperties(supported).Should().BeEquivalentTo(common.Concat(new[] { "externalJobSiteIntegrationId", "sourcePlatformId", "models", "aggregateVersion" }));
        JsonProperties(visibility).Should().BeEquivalentTo(common.Concat(new[] { "attributionVisibilityId", "actorId", "integrationId", "visibility", "aggregateVersion" }));
        JsonProperties(viewed).Should().BeEquivalentTo(common.Concat(new[] { "apiSchemaDocumentationId", "actorId", "apiVersion", "aggregateVersion" }));
    }

    [Fact]
    public void EveryPublishedErrorCode_HasAnEnglishAndAnArabicMessage()
    {
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!).Distinct();

        foreach (var code in codes)
        {
            JobPlatform.ExternalIntegration.Api.ExternalIntegrationErrorMessages.Catalog.Should().ContainKey(code);
            var (en, ar) = JobPlatform.ExternalIntegration.Api.ExternalIntegrationErrorMessages.Catalog[code];
            en.Should().NotBeNullOrWhiteSpace($"{code} needs an English message");
            ar.Should().NotBeNullOrWhiteSpace($"{code} needs an Arabic message");
            ar.Any(c => c is >= '؀' and <= 'ۿ').Should().BeTrue($"{code}'s Arabic message must be Arabic");
        }
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
