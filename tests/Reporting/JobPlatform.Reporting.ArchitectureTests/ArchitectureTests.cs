using System.Reflection;
using JobPlatform.Reporting.Application;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.Reporting;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;

namespace JobPlatform.Reporting.ArchitectureTests;

/// <summary>
/// Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row) for BC-12. Reporting is a read-model /
/// projection context: it owns no aggregates of its own domain lifecycle events other than its report-configuration aggregates (templates, schedules,
/// saved reports, exports, access rules, alert rules, labor-market reports) and it legitimately consumes ~45 other BCs' integration events in its
/// Application layer (event projectors) - that consumption is not a violation of the "no other bounded context" rule below, which is scoped to project
/// references, not to reading published event contracts.
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(FactEvent).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(ReportingDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string ApplicationNs = "JobPlatform.Reporting.Application";
    private const string InfrastructureNs = "JobPlatform.Reporting.Infrastructure";
    private const string ApiNs = "JobPlatform.Reporting.Api";

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
    /// Reporting's Application layer legitimately references ~45 other BCs' published integration-event contracts (its event projectors) - that is
    /// consuming a public contract, not depending on another bounded context's implementation. Only real project references (Domain/Application/
    /// Infrastructure/Api assemblies of another BC) are forbidden, which is exactly what <see cref="ArchitectureRules.ReferencedOtherBoundedContexts"/>
    /// checks (it inspects referenced *assemblies*, and every SharedKernel.IntegrationEvents.* contract lives in the SharedKernel assembly, not in
    /// another BC's own assembly).
    /// </summary>
    [Fact]
    public void Api_NeverReferencesAnotherBoundedContext() =>
        ArchitectureRules.ReferencedOtherBoundedContexts("Reporting", Domain, Application, Infrastructure, Api).Should().BeEmpty();

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

    /// <summary>
    /// Unlike a lifecycle BC, Reporting's aggregates are its own report-configuration entities (it owns no "posting"/"account"-style domain
    /// aggregate): the analytics Fact* rows are plain read-model entities written directly by the fact store, not aggregates with invariants.
    /// </summary>
    [Fact]
    public void AggregateRoots_AreSealed()
    {
        var roots = Domain.GetTypes().Where(t => InheritsFromGeneric(t, typeof(AggregateRoot<>))).ToList();

        roots.Select(r => r.Name).Should().BeEquivalentTo(
            "ActivityLogRetentionPolicy", "ReportTemplate", "ReportSchedule", "SavedReport", "ReportExport", "ReportAccessRule", "ReportAccessDecision",
            "PerformanceAlertRule", "PerformanceAlert", "LaborMarketReport");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    /// <summary>
    /// Reporting publishes only three integration events (PerformanceAlertRaised, ReportDistributionRequested to BC-13, and the access-decision
    /// audit record to BC-07) despite consuming forty-five. The published-events check below is scoped to this BC's own SharedKernel namespace
    /// only - never to the whole SharedKernel assembly, which now carries every other BC's published contracts too.
    /// </summary>
    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndOwnIntegrationEventsLiveInTheirOwnSharedKernelNamespace()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var ownIntegrationEvents = SharedKernel.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
            && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.Reporting").ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        ownIntegrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        ownIntegrationEvents.Should().HaveCount(2, "BC-12 itself publishes exactly two events (the audit record is BC-07's own contract, not Reporting's)");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract in its Application layer");
    }

    [Fact]
    public void IntegrationEventContracts_MatchTheFrozenSnapshot()
    {
        var alert = new PerformanceAlertRaisedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "error_rate_percent", "Critical", 9m, 5m, 1);
        var distribution = new ReportDistributionRequestedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "ref", new[] { "a@example.com" },
            "Pdf", 1);
        var common = new[] { "messageId", "occurredOnUtc", "correlationId", "causationId", "version" };

        JsonProperties(alert).Should().BeEquivalentTo(common.Concat(new[] { "alertId", "metric", "severity", "value", "threshold", "aggregateVersion" }));
        JsonProperties(distribution).Should().BeEquivalentTo(common.Concat(new[] { "scheduleId", "reportRef", "recipients", "format", "aggregateVersion" }));
    }

    /// <summary>
    /// Reporting's own event projectors legitimately reference every other BC's published `IntegrationEvents.*` namespace (that is the point of an
    /// ingestion/projection BC) - so, unlike a lifecycle BC, Application here is NOT expected to avoid those namespaces. What it must still avoid is
    /// depending on another BC's Domain/Application/Infrastructure/Api project.
    /// </summary>
    [Fact]
    public void Application_MayReferenceOtherBoundedContextsPublishedEvents_ButNeverTheirProjects()
    {
        var referencedProjects = Application.GetReferencedAssemblies().Select(r => r.Name ?? string.Empty)
            .Where(n => n.StartsWith("JobPlatform.", StringComparison.Ordinal) && !n.StartsWith("JobPlatform.SharedKernel", StringComparison.Ordinal)
                        && !n.StartsWith("JobPlatform.Reporting.", StringComparison.Ordinal)).ToArray();

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
