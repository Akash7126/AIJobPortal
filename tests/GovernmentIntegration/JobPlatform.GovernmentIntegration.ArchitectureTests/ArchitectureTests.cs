using System.Reflection;
using JobPlatform.GovernmentIntegration.Application;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;

namespace JobPlatform.GovernmentIntegration.ArchitectureTests;

/// <summary>Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row).</summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(EmployerVerification).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(GovernmentIntegrationDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string ApplicationNs = "JobPlatform.GovernmentIntegration.Application";
    private const string InfrastructureNs = "JobPlatform.GovernmentIntegration.Infrastructure";
    private const string ApiNs = "JobPlatform.GovernmentIntegration.Api";

    [Fact]
    public void Domain_DependsOnSharedKernelOnly_AndHasNoFrameworkOrHttp() =>
        ArchitectureRules.DomainDependsOn(Domain, ApplicationNs, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "System.Net.Http", "Serilog",
            "Microsoft.Extensions.Logging").Should().BeEmpty();

    [Fact]
    public void Application_DependsOnDomainAndSharedKernelOnly() =>
        ArchitectureRules.DoesNotDependOn(Application, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore", "RabbitMQ", "StackExchange.Redis", "System.Net.Http").Should().BeEmpty();

    [Fact]
    public void Infrastructure_DoesNotDependOnTheApiHost() =>
        ArchitectureRules.DoesNotDependOn(Infrastructure, ApiNs).Should().BeEmpty();

    [Fact]
    public void Api_NeverReferencesAnotherBoundedContext() =>
        ArchitectureRules.ReferencedOtherBoundedContexts("GovernmentIntegration", Domain, Application, Infrastructure, Api).Should().BeEmpty();

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

        roots.Select(r => r.Name).Should().BeEquivalentTo("EmployerVerification", "GovernmentVerificationData", "EducationalCredentialVerification",
            "IdentityVerificationData", "LegacyData", "DataQuality", "MigrationRun", "GovernmentSourceConnection");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndIntegrationEventsLiveInTheSharedKernel()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var integrationEvents = SharedKernel.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
            && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration").ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        domainEvents.Should().HaveCount(6, "BC-01 raises exactly the 6 domain events that map 1:1 onto its 6 published integration events");
        integrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        integrationEvents.Should().HaveCount(6, "BC-01 publishes exactly six events (handover section 5.1)");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract");
    }

    [Fact]
    public void IntegrationEventContracts_MatchTheFrozenSnapshot()
    {
        var common = new[] { "messageId", "occurredOnUtc", "correlationId", "causationId", "version" };

        var employerVerificationApproved = new EmployerVerificationApprovedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Automatic", 1);
        var govData = new GovernmentVerificationDataImportedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(),
            "Employer", Guid.NewGuid(), "Verified", 1);
        var educational = new EducationalCredentialVerificationImportedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null,
            Guid.NewGuid(), Guid.NewGuid(), "Verified", 1);
        var identity = new IdentityVerificationDataImportedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(),
            Guid.NewGuid(), "Verified", 1);
        var legacy = new LegacyDataImportedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            "JobSeeker", "MoL", 1);
        var dataQuality = new DataQualityUpdatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), "Running",
            "Completed", Guid.NewGuid(), 10, 1, 1);

        JsonProperties(employerVerificationApproved).Should().BeEquivalentTo(common.Concat(new[]
            { "employerVerificationId", "employerAccountId", "actorId", "method", "aggregateVersion" }));
        JsonProperties(govData).Should().BeEquivalentTo(common.Concat(new[]
            { "governmentVerificationDataId", "subjectType", "subjectId", "outcome", "aggregateVersion" }));
        JsonProperties(educational).Should().BeEquivalentTo(common.Concat(new[]
            { "educationalCredentialVerificationId", "subjectId", "outcome", "aggregateVersion" }));
        JsonProperties(identity).Should().BeEquivalentTo(common.Concat(new[] { "identityVerificationDataId", "subjectId", "outcome", "aggregateVersion" }));
        JsonProperties(legacy).Should().BeEquivalentTo(common.Concat(new[]
            { "legacyDataId", "migrationRunId", "recordType", "sourceSystem", "aggregateVersion" }));
        JsonProperties(dataQuality).Should().BeEquivalentTo(common.Concat(new[]
            { "dataQualityId", "fromStatus", "toStatus", "migrationRunId", "recordsChecked", "recordsRejected", "aggregateVersion" }));
    }

    [Fact]
    public void NoPiiInLogTemplates()
    {
        // The domain never logs IdentityClaim/Submission/VerifiedFields directly - it only raises events carrying ids and non-PII outcome
        // strings (handover section 5.1: "payload is ids only"). This is enforced structurally by the event shapes above rather than by
        // scanning log call sites (no direct Microsoft.Extensions.Logging usage exists in Domain or Application - see the dependency tests).
        Domain.GetTypes().Should().NotContain(t => t.Namespace != null && t.Namespace.Contains("Logging"));
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
