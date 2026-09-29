using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobPlatform.AccountIdentity.Application;
using JobPlatform.AccountIdentity.Domain.Accounts;
using JobPlatform.AccountIdentity.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Common.Enums;
using JobPlatform.SharedKernel.Domain;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;
using JobPlatform.TestSupport;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace JobPlatform.AccountIdentity.ArchitectureTests;

/// <summary>Enforces the dependency rule and the coding rules of foundation sections 3, 4 and 14.1 (architecture row).</summary>
public class ArchitectureTests
{
    private static readonly Assembly SharedKernel = typeof(IDomainEvent).Assembly;
    private static readonly Assembly Domain = typeof(Account).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(IdentityDbContext).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;
    private static readonly Assembly BuildingBlocks = typeof(JobPlatform.BuildingBlocks.Infrastructure.Persistence.BaseDbContext).Assembly;

    private const string DomainNs = "JobPlatform.AccountIdentity.Domain";
    private const string ApplicationNs = "JobPlatform.AccountIdentity.Application";
    private const string InfrastructureNs = "JobPlatform.AccountIdentity.Infrastructure";
    private const string ApiNs = "JobPlatform.AccountIdentity.Api";

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.Should().BeTrue("these types depend on a forbidden namespace: " + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    // ------------------------------------------------------------------ dependency rule (foundation section 4)

    [Fact]
    public void Domain_DependsOnSharedKernelOnly_AndHasNoFrameworkOrCryptoOrHttp() =>
        AssertNoDependency(Domain, ApplicationNs, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "FluentValidation", "RabbitMQ", "StackExchange.Redis", "System.Net.Http", "System.Security.Cryptography", "Serilog", "Microsoft.Extensions.Logging");

    [Fact]
    public void Application_DependsOnDomainAndSharedKernelOnly() =>
        AssertNoDependency(Application, InfrastructureNs, ApiNs, "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "RabbitMQ", "StackExchange.Redis", "System.Net.Http", "System.Security.Cryptography");

    [Fact]
    public void Infrastructure_DoesNotDependOnTheApiHost() => AssertNoDependency(Infrastructure, ApiNs);

    [Fact]
    public void SharedKernel_ContainsContractsOnly_NoBcAssembliesNoEfNoAspNet() =>
        AssertNoDependency(SharedKernel, "JobPlatform.AccountIdentity", "JobPlatform.BuildingBlocks", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "RabbitMQ", "StackExchange.Redis", "FluentValidation");

    [Fact]
    public void BuildingBlocks_KnowsNoBoundedContext() => AssertNoDependency(BuildingBlocks, "JobPlatform.AccountIdentity");

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
    public void Domain_ReferencesOnlySharedKernel() =>
        PlatformReferences(Domain).Should().BeSubsetOf(new[] { "JobPlatform.SharedKernel" });

    [Fact]
    public void Application_ReferencesOnlyDomainAndSharedKernel() =>
        PlatformReferences(Application).Should().BeSubsetOf(new[] { "JobPlatform.AccountIdentity.Domain", "JobPlatform.SharedKernel" });

    [Fact]
    public void Infrastructure_ReferencesOnlyItsOwnLayersSharedKernelAndBuildingBlocks() =>
        PlatformReferences(Infrastructure).Should().BeSubsetOf(new[]
        {
            "JobPlatform.AccountIdentity.Application", "JobPlatform.AccountIdentity.Domain", "JobPlatform.SharedKernel", "JobPlatform.BuildingBlocks.Infrastructure"
        });

    [Fact]
    public void Api_NeverReferencesAnotherBoundedContext() =>
        PlatformReferences(Api).Should().OnlyContain(n => n.StartsWith("JobPlatform.AccountIdentity.") || n == "JobPlatform.SharedKernel" || n.StartsWith("JobPlatform.BuildingBlocks."));

    // ------------------------------------------------------------------ tactical rules

    [Fact]
    public void AggregatesAndEntities_HaveNoPublicSetters()
    {
        var childEntities = new[] { "ActivationChallenge", "AccountStatusChange", "AccountRoleAssignment", "RolePermission", "UserSession" };
        var types = Domain.GetTypes().Where(t => t.IsClass && !t.IsAbstract &&
            (InheritsFromGeneric(t, typeof(Entity<>)) || t.IsSubclassOf(typeof(ValueObject)) || childEntities.Contains(t.Name))).ToList();

        types.Should().NotBeEmpty();
        var offenders = types
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (Type: t, Property: p)))
            .Where(x => x.Property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
            .Select(x => $"{x.Type.Name}.{x.Property.Name}")
            .ToList();

        offenders.Should().BeEmpty("state changes only through behaviour methods; offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AggregateRoots_ExposeBehaviour_AndAreSealed()
    {
        var roots = Domain.GetTypes().Where(t => InheritsFromGeneric(t, typeof(AggregateRoot<>))).ToList();

        roots.Select(r => r.Name).Should().BeEquivalentTo("Account", "ApiCredential", "PasswordPolicy", "Role", "SessionTimeoutSetting", "PrivacyConsent");
        roots.Should().OnlyContain(r => r.IsSealed);
    }

    [Fact]
    public void CommandsQueriesAndHandlers_FollowTheFeatureFolders_OneHandlerPerRequest() =>
        ArchitectureRules.CqrsLayoutViolations(Application).Should().BeEmpty();

    [Fact]
    public void Validators_LiveInTheValidatorsFolder()
    {
        var validatorsNs = Application.GetName().Name + ".Validators";
        Application.GetTypes().Where(t => t.IsClass && InheritsFromGeneric(t, typeof(FluentValidation.AbstractValidator<>)))
            .Where(t => t.Namespace is null || !(t.Namespace == validatorsNs || t.Namespace.StartsWith(validatorsNs + ".", StringComparison.Ordinal)))
            .Select(t => t.FullName).Should().BeEmpty();
    }

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
        // Scoped to this BC's own namespace: the SharedKernel assembly now also carries every other BC's integration events
        // (BC-04..BC-13), so scanning the whole assembly would fail this test on contracts AccountIdentity does not own.
        var integrationEvents = SharedKernel.GetTypes()
            .Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract
                && t.Namespace == "JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity")
            .ToList();

        domainEvents.Should().OnlyContain(e => e.IsSealed && e.Name.EndsWith("DomainEvent"));
        integrationEvents.Should().OnlyContain(e => e.Name.EndsWith("IntegrationEvent"));
        integrationEvents.Should().HaveCount(5, "BC-03 publishes exactly five events");
        Application.GetTypes().Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)).Should().BeEmpty("no BC defines its own published contract");
    }

    [Fact]
    public void ApiCredentialCreated_PayloadHasNoFieldThatCouldCarryASecret()
    {
        var payload = new ApiCredentialCreatedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, 1);

        JsonDocument.Parse(IntegrationJson.Serialize(payload)).RootElement.EnumerateObject().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("secret", StringComparison.OrdinalIgnoreCase) || n.Contains("hash", StringComparison.OrdinalIgnoreCase)
                                      || n.Contains("key", StringComparison.OrdinalIgnoreCase) || n.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------ published-contract snapshot (foundation 14.1: contract tests)

    [Fact]
    public void IntegrationEventContracts_MatchTheFrozenSnapshot()
    {
        var context = (Guid.NewGuid(), (Guid?)null, DateTime.UtcNow, Guid.NewGuid());
        IIntegrationEvent[] events =
        {
            new AccountCreatedIntegrationEvent(Guid.NewGuid(), context.Item3, context.Item4, null, Guid.NewGuid(), Guid.NewGuid(), ActorType.JobSeeker, 1),
            new AccountApprovedIntegrationEvent(Guid.NewGuid(), context.Item3, context.Item4, null, Guid.NewGuid(), Guid.NewGuid(), ActorType.JobSeeker, 1),
            new AccountSuspendedIntegrationEvent(Guid.NewGuid(), context.Item3, context.Item4, null, Guid.NewGuid(), Guid.NewGuid(), ActorType.JobSeeker, "r", "Deactivated", 1),
            new ApiCredentialCreatedIntegrationEvent(Guid.NewGuid(), context.Item3, context.Item4, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), context.Item3, 1),
            new UserAccountApprovedIntegrationEvent(Guid.NewGuid(), context.Item3, context.Item4, null, Guid.NewGuid(), Guid.NewGuid(), "Pending", "Active", 1)
        };
        var common = new[] { "messageId", "occurredOnUtc", "correlationId", "causationId", "version" };
        var expected = new Dictionary<string, string[]>
        {
            ["AccountCreated"] = common.Concat(new[] { "accountId", "actorId", "actorType", "aggregateVersion" }).ToArray(),
            ["AccountApproved"] = common.Concat(new[] { "accountId", "actorId", "actorType", "aggregateVersion" }).ToArray(),
            ["AccountSuspended"] = common.Concat(new[] { "accountId", "actorId", "actorType", "reason", "standing", "aggregateVersion" }).ToArray(),
            ["ApiCredentialCreated"] = common.Concat(new[] { "apiCredentialId", "accountId", "actorId", "expiresAtUtc", "aggregateVersion" }).ToArray(),
            ["UserAccountApproved"] = common.Concat(new[] { "userAccountId", "actorId", "fromStanding", "toStanding", "aggregateVersion" }).ToArray()
        };

        foreach (var integrationEvent in events)
        {
            var properties = JsonDocument.Parse(IntegrationJson.Serialize(integrationEvent)).RootElement.EnumerateObject().Select(p => p.Name);

            properties.Should().BeEquivalentTo(expected[integrationEvent.EventType], $"{integrationEvent.EventType}: removing or renaming a field is a breaking change (add a V2 instead)");
        }
    }

    // ------------------------------------------------------------------ source-level rules

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JobPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("JobPlatform.sln not found above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<string> SourceFiles(string relative) =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), relative), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"));

    [Theory]
    [InlineData("src/Services/AccountIdentity/JobPlatform.AccountIdentity.Domain")]
    [InlineData("src/Services/AccountIdentity/JobPlatform.AccountIdentity.Application")]
    [InlineData("src/BuildingBlocks/JobPlatform.SharedKernel")]
    public void NoAmbientClock_TimeAlwaysComesFromTimeProvider(string project)
    {
        var offenders = SourceFiles(project)
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: Path.GetFileName(f), Line: i + 1, Text: line)))
            .Where(l => Regex.IsMatch(l.Text, @"\b(DateTime|DateTimeOffset)\.(UtcNow|Now|Today)\b") && !l.Text.TrimStart().StartsWith("//"))
            .Select(l => $"{l.File}:{l.Line}").ToList();

        offenders.Should().BeEmpty("foundation section 8: no DateTime.Now/UtcNow - inject TimeProvider");
    }

    [Fact]
    public void Logging_NeverPassesSecretsOrTokensAsArguments()
    {
        var forbidden = new Regex(@"\b(SecretHash|PasswordHash|ClientSecret|clientSecret|RefreshToken|refreshToken|AccessToken|MfaSecret|CurrentPassword|NewPassword|request\.Password|\.Password\b)\b");
        var statements = new List<string>();
        foreach (var file in SourceFiles("src"))
        {
            var text = File.ReadAllText(file);
            statements.AddRange(Regex.Matches(text, @"_logger\.Log\w+\([^;]*;", RegexOptions.Singleline).Select(m => $"{Path.GetFileName(file)}: {m.Value}"));
        }

        statements.Should().NotBeEmpty();
        statements.Where(s => forbidden.IsMatch(s)).Should().BeEmpty("secrets, tokens and password hashes must never reach a log statement");
    }

    [Fact]
    public void Domain_HasNoLoggingAndNoConsole()
    {
        var offenders = SourceFiles("src/Services/AccountIdentity/JobPlatform.AccountIdentity.Domain")
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"ILogger|Console\.|Debug\.Write")).Select(Path.GetFileName).ToList();

        offenders.Should().BeEmpty();
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
