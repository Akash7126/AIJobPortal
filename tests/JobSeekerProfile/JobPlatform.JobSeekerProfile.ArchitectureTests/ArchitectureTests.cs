using System.Reflection;
using JobPlatform.JobSeekerProfile.Application;
using JobPlatform.JobSeekerProfile.Domain;
using JobPlatform.JobSeekerProfile.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using JobPlatform.TestSupport;

namespace JobPlatform.JobSeekerProfile.ArchitectureTests;

/// <summary>Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row) for BC-04.</summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(Profile).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(JobSeekerProfileDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string ApplicationNs = "JobPlatform.JobSeekerProfile.Application";
    private const string InfrastructureNs = "JobPlatform.JobSeekerProfile.Infrastructure";
    private const string ApiNs = "JobPlatform.JobSeekerProfile.Api";

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
        ArchitectureRules.ReferencedOtherBoundedContexts("JobSeekerProfile", Domain, Application, Infrastructure, Api).Should().BeEmpty();

    [Fact]
    public void Repositories_LiveInThePersistenceRepositoriesFolder() =>
        ArchitectureRules.RepositoriesOutsideRepositoriesFolder(Infrastructure).Should().BeEmpty();

    [Fact]
    public void Interfaces_LiveInTheInterfacesFolderOfTheirLayer() =>
        ArchitectureRules.InterfacesOutsideInterfacesFolders(Domain, Application, Infrastructure, Api).Should().BeEmpty();

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

        roots.Select(r => r.Name).Should().BeEquivalentTo("Profile", "ProfileShareLink", "Resume", "SupplementaryDocument");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    [Fact]
    public void Validators_AreNamedValidator()
    {
        var validatorBase = Application.GetTypes().SelectMany(t => t.BaseType is { IsGenericType: true } bt
            && bt.GetGenericTypeDefinition().Name.StartsWith("AbstractValidator") ? new[] { t } : Array.Empty<Type>()).ToList();

        validatorBase.Should().NotBeEmpty();
        validatorBase.Should().OnlyContain(v => v.Name.EndsWith("Validator"));
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndIntegrationEventsLiveInTheSharedKernel()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var integrationEvents = SharedKernel.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
            && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile").ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        integrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        integrationEvents.Should().HaveCount(3, "BC-04 publishes ProfileCreated, ProfileUpdated and ResumeCreated");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract");
    }

    [Fact]
    public void IntegrationEventContracts_MatchTheFrozenSnapshot()
    {
        var created = new ProfileCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Ramallah", 1);
        var updated = new ProfileUpdatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "Active", "Active",
            Guid.NewGuid(), new[] { "Level1" }, 50, 1);
        var resume = new ResumeCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Pdf", 100, "abc123", 1);
        var common = new[] { "messageId", "occurredOnUtc", "correlationId", "causationId", "version" };

        JsonProperties(created).Should().BeEquivalentTo(common.Concat(new[] { "profileId", "actorId", "ownerAccountId", "governorate", "aggregateVersion" }));
        JsonProperties(updated).Should().BeEquivalentTo(common.Concat(new[]
            { "profileId", "fromStatus", "toStatus", "actorId", "changedSections", "completionPercent", "aggregateVersion" }));
        JsonProperties(resume).Should().BeEquivalentTo(common.Concat(new[]
            { "resumeId", "actorId", "profileId", "format", "sizeBytes", "sha256", "aggregateVersion" }));
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
