using System.Reflection;
using JobPlatform.JobPosting.Application;
using JobPlatform.JobPosting.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.Domain.Interfaces;
using JobPlatform.SharedKernel.Messaging.Interfaces;
using JobPlatform.TestSupport;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace JobPlatform.JobPosting.ArchitectureTests;

/// <summary>Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row).</summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(JobPlatform.JobPosting.Domain.JobPosting).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(JobPostingDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly Assembly BuildingBlocks = typeof(JobPlatform.BuildingBlocks.Infrastructure.Persistence.BaseDbContext).Assembly;

    private const string DomainNs = "JobPlatform.JobPosting.Domain";
    private const string ApplicationNs = "JobPlatform.JobPosting.Application";
    private const string InfrastructureNs = "JobPlatform.JobPosting.Infrastructure";
    private const string ApiNs = "JobPlatform.JobPosting.Api";

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.Should().BeTrue("these types depend on a forbidden namespace: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Domain_DependsOnSharedKernelOnly_AndHasNoFrameworkOrHttp() =>
        AssertNoDependency(Domain, ApplicationNs, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "FluentValidation", "RabbitMQ", "StackExchange.Redis", "System.Net.Http", "Serilog", "Microsoft.Extensions.Logging");

    [Fact]
    public void Application_DependsOnDomainAndSharedKernelOnly() =>
        AssertNoDependency(Application, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "RabbitMQ", "StackExchange.Redis", "System.Net.Http");

    [Fact]
    public void Infrastructure_DoesNotDependOnTheApiHost() => AssertNoDependency(Infrastructure, ApiNs);

    [Fact]
    public void Controllers_HoldNoDbContext_NoDomain_NoInfrastructure()
    {
        var result = Types.InAssembly(Api).That().Inherit(typeof(ControllerBase)).ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", DomainNs, InfrastructureNs + ".Persistence", "JobPlatform.BuildingBlocks.Infrastructure.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("controllers are thin: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    // ------------------------------------------------------------------ "no other BC references"

    private static IEnumerable<string> PlatformReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("JobPlatform.", StringComparison.Ordinal)).OrderBy(n => n);

    [Fact]
    public void Domain_ReferencesOnlySharedKernel() => PlatformReferences(Domain).Should().BeSubsetOf(new[] { "JobPlatform.SharedKernel" });

    [Fact]
    public void Application_ReferencesOnlyDomainAndSharedKernel() =>
        PlatformReferences(Application).Should().BeSubsetOf(new[] { "JobPlatform.JobPosting.Domain", "JobPlatform.SharedKernel" });

    [Fact]
    public void Infrastructure_ReferencesOnlyItsOwnLayersSharedKernelAndBuildingBlocks() =>
        PlatformReferences(Infrastructure).Should().BeSubsetOf(new[]
        {
            "JobPlatform.JobPosting.Application", "JobPlatform.JobPosting.Domain", "JobPlatform.SharedKernel", "JobPlatform.BuildingBlocks.Infrastructure"
        });

    [Fact]
    public void Api_NeverReferencesAnotherBoundedContext() =>
        PlatformReferences(Api).Should().OnlyContain(n => n.StartsWith("JobPlatform.JobPosting.") || n == "JobPlatform.SharedKernel" || n.StartsWith("JobPlatform.BuildingBlocks."));

    [Fact]
    public void SharedKernel_ContainsContractsOnly_NoBcAssembliesNoEfNoAspNet() =>
        AssertNoDependency(SharedKernel, "JobPlatform.JobPosting", "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "RabbitMQ", "StackExchange.Redis", "FluentValidation");

    [Fact]
    public void BuildingBlocks_KnowsNoBoundedContext() => AssertNoDependency(BuildingBlocks, "JobPlatform.JobPosting");

    // ------------------------------------------------------------------ tactical rules

    [Fact]
    public void AggregatesAndEntities_HaveNoPublicSetters()
    {
        var types = Domain.GetTypes().Where(t => t.IsClass && !t.IsAbstract && InheritsFromGeneric(t, typeof(Entity<>))).ToList();

        types.Should().NotBeEmpty();
        var offenders = types
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        offenders.Should().BeEmpty("state changes only through behaviour methods; offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AggregateRoots_AreSealed()
    {
        var roots = Domain.GetTypes().Where(t => InheritsFromGeneric(t, typeof(AggregateRoot<>))).ToList();

        roots.Select(r => r.Name).Should().BeEquivalentTo("JobPosting", "FavoriteJobList", "SavedSearch", "InterestedListEntry");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

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
    public void Handlers_AreInternalAndSealed()
    {
        var handlers = Application.GetTypes().Where(t => t.IsClass && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))).ToList();

        handlers.Should().NotBeEmpty();
        handlers.Should().OnlyContain(h => !h.IsPublic && h.IsSealed, "handlers are an implementation detail reached only through the dispatcher");
    }

    [Fact]
    public void EveryRequest_HasAHandler_AndACommandOrQueryName()
    {
        var requests = Application.GetTypes().Where(t => t.IsClass && !t.IsAbstract &&
            t.GetInterfaces().Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICommand<>) || i.GetGenericTypeDefinition() == typeof(IQuery<>)))).ToList();
        var handled = Application.GetTypes().SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)).Select(i => i.GetGenericArguments()[0]).ToHashSet();

        requests.Should().NotBeEmpty();
        requests.Where(r => !handled.Contains(r)).Select(r => r.Name).Should().BeEmpty("every command/query needs a handler");
        requests.Should().OnlyContain(r => r.Name.EndsWith("Command") || r.Name.EndsWith("Query"));
    }

    [Fact]
    public void Validators_AreNamedAfterTheirRequest()
    {
        var validators = Application.GetTypes().Where(t => t.IsClass && !t.IsAbstract && InheritsFromGeneric(t, typeof(FluentValidation.AbstractValidator<>))).ToList();

        validators.Should().NotBeEmpty();
        validators.Should().OnlyContain(v => v.Name.EndsWith("Validator"));
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndIntegrationEventsLiveInTheSharedKernel()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var integrationEvents = SharedKernel.GetTypes()
            .Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.JobPosting")
            .ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        integrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        integrationEvents.Should().HaveCount(7, "BC-09 publishes exactly seven events");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract");
    }

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

    private static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}
