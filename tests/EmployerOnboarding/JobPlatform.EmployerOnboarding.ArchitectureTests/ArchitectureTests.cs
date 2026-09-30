using System.Reflection;
using JobPlatform.EmployerOnboarding.Application;
using JobPlatform.EmployerOnboarding.Domain;
using JobPlatform.EmployerOnboarding.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using JobPlatform.TestSupport;

namespace JobPlatform.EmployerOnboarding.ArchitectureTests;

/// <summary>Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row).</summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(EmployerRegistration).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(EmployerOnboardingDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly Assembly BuildingBlocks = typeof(JobPlatform.BuildingBlocks.Infrastructure.Persistence.BaseDbContext).Assembly;

    private const string ApplicationNs = "JobPlatform.EmployerOnboarding.Application";
    private const string InfrastructureNs = "JobPlatform.EmployerOnboarding.Infrastructure";
    private const string ApiNs = "JobPlatform.EmployerOnboarding.Api";

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
        ArchitectureRules.ReferencedOtherBoundedContexts("EmployerOnboarding", Domain, Application, Infrastructure, Api).Should().BeEmpty();

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

        roots.Select(r => r.Name).Should().BeEquivalentTo("EmployerRegistration", "CompanyMediaAndDocument", "EmployerStanding");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndIntegrationEventsLiveInTheSharedKernel()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var integrationEvents = SharedKernel.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
            && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding").ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        integrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        integrationEvents.Should().HaveCount(2, "BC-05 publishes exactly two events");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract");
    }

    [Fact]
    public void IntegrationEventContracts_MatchTheFrozenSnapshot()
    {
        var approved = new EmployerRegistrationApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1);
        var media = new CompanyMediaAndDocumentCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Logo", 1);
        var common = new[] { "messageId", "occurredOnUtc", "correlationId", "causationId", "version" };

        JsonProperties(approved).Should().BeEquivalentTo(common.Concat(new[] { "employerRegistrationId", "actorId", "employerAccountId", "aggregateVersion" }));
        JsonProperties(media).Should().BeEquivalentTo(common.Concat(new[] { "companyMediaAndDocumentId", "actorId", "employerAccountId", "kind", "aggregateVersion" }));
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
