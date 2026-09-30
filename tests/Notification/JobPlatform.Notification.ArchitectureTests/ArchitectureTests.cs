using System.Reflection;
using JobPlatform.Notification.Application;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using JobPlatform.TestSupport;

namespace JobPlatform.Notification.ArchitectureTests;

/// <summary>
/// Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row) for BC-13. Like Reporting, Notification's
/// Application layer legitimately references a handful of other BCs' published integration-event contracts (SavedSearchMatched, JobRecommendationComputed,
/// JobDataImported, AccountApproved, AccountSuspended) because it reacts to them - that is consuming a public contract, not depending on another bounded
/// context's implementation.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(InAppNotification).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(NotificationDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string ApplicationNs = "JobPlatform.Notification.Application";
    private const string InfrastructureNs = "JobPlatform.Notification.Infrastructure";
    private const string ApiNs = "JobPlatform.Notification.Api";

    [Fact]
    public void Domain_DependsOnSharedKernelOnly_AndHasNoFrameworkOrHttp() =>
        ArchitectureRules.DomainDependsOn(Domain, ApplicationNs, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "System.Net.Http", "Serilog", "Microsoft.Extensions.Logging")
            .Should().BeEmpty();

    [Fact]
    public void Application_DependsOnDomainAndSharedKernelOnly() =>
        ArchitectureRules.DoesNotDependOn(Application, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore", "RabbitMQ", "StackExchange.Redis").Should().BeEmpty();

    [Fact]
    public void Infrastructure_DoesNotDependOnTheApiHost() =>
        ArchitectureRules.DoesNotDependOn(Infrastructure, ApiNs).Should().BeEmpty();

    /// <summary>
    /// Only real project references (another BC's own Domain/Application/Infrastructure/Api assembly) are forbidden; every consumed event contract of
    /// another BC lives in the shared SharedKernel assembly, so referencing it here is not "another bounded context".
    /// </summary>
    [Fact]
    public void Api_NeverReferencesAnotherBoundedContext() =>
        ArchitectureRules.ReferencedOtherBoundedContexts("Notification", Domain, Application, Infrastructure, Api).Should().BeEmpty();

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

        // NotificationPreference, EmailTemplate, NotificationType, SmsPolicy and WeeklyCycle are plain entities (last-write-wins settings/config,
        // not aggregates with their own invariant-guarded lifecycle events), so they are intentionally excluded here.
        roots.Select(r => r.Name).Should().BeEquivalentTo("InAppNotification", "OutboundMessage", "JobConfirmation");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndOwnIntegrationEventsLiveInTheirOwnSharedKernelNamespace()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var ownIntegrationEvents = SharedKernel.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
            && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.Notification").ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        ownIntegrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        ownIntegrationEvents.Should().HaveCount(3, "BC-13 publishes exactly three events (NotificationSent, NotificationStatusUpdated, JobConfirmationSent)");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract in its Application layer");
    }

    [Fact]
    public void IntegrationEventContracts_MatchTheFrozenSnapshot()
    {
        var sent = new NotificationSentIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "Email", "Welcome", Guid.NewGuid(),
            "j***@example.com", "Welcome", "Sent", 1);
        var statusUpdated = new NotificationStatusUpdatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "Unread", "Read", Guid.NewGuid(), 1);
        var confirmed = new JobConfirmationSentIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "job-1", 1);
        var common = new[] { "messageId", "occurredOnUtc", "correlationId", "causationId", "version" };

        JsonProperties(sent).Should().BeEquivalentTo(
            common.Concat(new[] { "notificationId", "channel", "category", "recipientAccountId", "maskedRecipient", "subject", "status", "aggregateVersion" }));
        JsonProperties(statusUpdated).Should().BeEquivalentTo(
            common.Concat(new[] { "notificationStatusId", "fromStatus", "toStatus", "recipientAccountId", "aggregateVersion" }));
        JsonProperties(confirmed).Should().BeEquivalentTo(common.Concat(new[] { "jobConfirmationId", "actorId", "sourcePlatformId", "platformJobId", "aggregateVersion" }));
    }

    /// <summary>
    /// Notification's own inbox handlers legitimately reference the handful of other BCs' published `IntegrationEvents.*` namespaces they react to - what
    /// must still never happen is depending on another BC's own Domain/Application/Infrastructure/Api project.
    /// </summary>
    [Fact]
    public void Application_MayReferenceOtherBoundedContextsPublishedEvents_ButNeverTheirProjects()
    {
        var referencedProjects = Application.GetReferencedAssemblies().Select(r => r.Name ?? string.Empty)
            .Where(n => n.StartsWith("JobPlatform.", StringComparison.Ordinal) && !n.StartsWith("JobPlatform.SharedKernel", StringComparison.Ordinal)
                        && !n.StartsWith("JobPlatform.Notification.", StringComparison.Ordinal)).ToArray();

        referencedProjects.Should().BeEmpty("Application may consume other BCs' event contracts (they live in SharedKernel) but must never reference another BC's own assembly");
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
