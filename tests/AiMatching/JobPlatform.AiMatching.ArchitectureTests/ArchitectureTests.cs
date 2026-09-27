using System.Reflection;
using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace JobPlatform.AiMatching.ArchitectureTests;

/// <summary>Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row) for BC-10.</summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(MatchScore).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(AiMatchingDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private const string DomainNs = "JobPlatform.AiMatching.Domain";
    private const string ApplicationNs = "JobPlatform.AiMatching.Application";
    private const string InfrastructureNs = "JobPlatform.AiMatching.Infrastructure";
    private const string ApiNs = "JobPlatform.AiMatching.Api";

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
            "RabbitMQ", "StackExchange.Redis");

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

    private static IEnumerable<string> PlatformReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("JobPlatform.", StringComparison.Ordinal)).OrderBy(n => n);

    [Fact]
    public void Domain_ReferencesOnlySharedKernel() =>
        PlatformReferences(Domain).Should().BeSubsetOf(new[] { "JobPlatform.SharedKernel" });

    [Fact]
    public void Application_ReferencesOnlyDomainAndSharedKernel() =>
        PlatformReferences(Application).Should().BeSubsetOf(new[] { "JobPlatform.AiMatching.Domain", "JobPlatform.SharedKernel" });

    [Fact]
    public void Api_NeverReferencesAnotherBoundedContext() =>
        PlatformReferences(Api).Should().OnlyContain(n => n.StartsWith("JobPlatform.AiMatching.") || n == "JobPlatform.SharedKernel" || n.StartsWith("JobPlatform.BuildingBlocks."));

    [Fact]
    public void AggregateRoots_AreSealed()
    {
        var roots = Domain.GetTypes().Where(t => InheritsFromGeneric(t, typeof(AggregateRoot<>))).ToList();

        roots.Select(r => r.Name).Should().BeEquivalentTo("MatchingConfiguration", "MatchScore", "ResumeParsedData", "SkillStandardization",
            "ParsedProfileData", "JobSemantics", "JobRecommendation", "CandidateShortlist");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    [Fact]
    public void AggregatesAndEntities_HaveNoPublicSetters()
    {
        var types = Domain.GetTypes().Where(t => t.IsClass && !t.IsAbstract && (InheritsFromGeneric(t, typeof(Entity<>)) || t.IsSubclassOf(typeof(ValueObject)))).ToList();

        types.Should().NotBeEmpty();
        var offenders = types
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        offenders.Should().BeEmpty("state changes only through behaviour methods; offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Handlers_AreInternalAndSealed()
    {
        var handlers = Application.GetTypes().Where(t => t.IsClass && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))).ToList();

        handlers.Should().NotBeEmpty();
        handlers.Should().OnlyContain(h => !h.IsPublic && h.IsSealed, "handlers are an implementation detail reached only through the dispatcher");
    }

    [Fact]
    public void Validators_AreNamedAfterTheirRequest()
    {
        var validators = Application.GetTypes().Where(t => t.IsClass && !t.IsAbstract && InheritsFromGeneric(t, typeof(FluentValidation.AbstractValidator<>))).ToList();

        validators.Should().NotBeEmpty();
        validators.Should().OnlyContain(v => v.Name.EndsWith("Validator"));
    }

    [Fact]
    public void DomainEvents_AreSealedRecordsNamedDomainEvent_AndPublishedEventsLiveInTheSharedKernel()
    {
        var domainEvents = Domain.GetTypes().Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        var integrationEvents = SharedKernel.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
            && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.AiMatching").ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        integrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        integrationEvents.Should().HaveCount(5, "BC-10 publishes exactly five events");
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
