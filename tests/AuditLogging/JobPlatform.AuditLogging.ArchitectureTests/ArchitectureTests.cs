using System.Reflection;
using JobPlatform.AuditLogging.Api;
using JobPlatform.AuditLogging.Application;
using JobPlatform.AuditLogging.Domain;
using JobPlatform.AuditLogging.Infrastructure;
using JobPlatform.TestSupport;

namespace JobPlatform.AuditLogging.ArchitectureTests;

/// <summary>Structural rules of foundation sections 4 and 14.1 for BC-07 (shared rules live in JobPlatform.TestSupport).</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(AuditEntry).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(AuditErrorMessages).Assembly;

    private const string Bc = "AuditLogging";

    [Fact]
    public void Domain_DependsOnNothingButTheSharedKernel() =>
        ArchitectureRules.DomainDependsOn(Domain, $"JobPlatform.{Bc}.Application", $"JobPlatform.{Bc}.Infrastructure", $"JobPlatform.{Bc}.Api", "JobPlatform.BuildingBlocks")
            .Should().BeEmpty();

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApiOrFrameworks() =>
        ArchitectureRules.DoesNotDependOn(Application, $"JobPlatform.{Bc}.Infrastructure", $"JobPlatform.{Bc}.Api", "Microsoft.EntityFrameworkCore", "RabbitMQ", "StackExchange.Redis",
            "Microsoft.AspNetCore", "JobPlatform.BuildingBlocks").Should().BeEmpty();

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        ArchitectureRules.DoesNotDependOn(Infrastructure, $"JobPlatform.{Bc}.Api").Should().BeEmpty();

    [Fact]
    public void NoBoundedContext_ReferencesAnotherOne() =>
        ArchitectureRules.ReferencedOtherBoundedContexts(Bc, Domain, Application, Infrastructure, Api).Should().BeEmpty();

    [Fact]
    public void CommandsQueriesAndHandlers_FollowTheFeatureFolders_OneHandlerPerRequest() =>
        ArchitectureRules.CqrsLayoutViolations(Application).Should().BeEmpty();

    [Fact]
    public void Validators_LiveInTheValidatorsFolder() => ArchitectureRules.ValidatorsOutsideValidatorsFolder(Application).Should().BeEmpty();

    [Fact]
    public void RequestHandlers_AreInternalSealed() => ArchitectureRules.HandlersNotInternalSealed(Application).Should().BeEmpty();

    [Fact]
    public void Aggregates_HaveNoPublicSetters() => ArchitectureRules.PublicSettersInDomain(Domain).Should().BeEmpty();

    [Fact]
    public void Controllers_DoNotUseTheDbContext() => ArchitectureRules.ControllersUsingPersistence(Api).Should().BeEmpty();

    [Fact]
    public void EveryPublishedErrorCode_HasAnEnglishAndAnArabicMessage()
    {
        var codes = typeof(AuditErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!)
            .Concat(new[] { "E-AUDIT-EXPORT-DUPLICATE", "E-AUDIT-REPORT-SOURCE-UNAVAILABLE" });

        foreach (var code in codes)
        {
            AuditErrorMessages.Catalog.Should().ContainKey(code);
            var (en, ar) = AuditErrorMessages.Catalog[code];
            en.Should().NotBeNullOrWhiteSpace($"{code} needs an English message");
            ar.Should().NotBeNullOrWhiteSpace($"{code} needs an Arabic message");
            ar.Any(c => c is >= '؀' and <= 'ۿ').Should().BeTrue($"{code}'s Arabic message must be Arabic");
        }
    }

    [Fact]
    public void ThisServicePublishesNoIntegrationEvents()
    {
        // BC-07 is a read-model context (handover 5.1): no mapper, no outbox processor, only consumers.
        Infrastructure.GetTypes().Concat(Application.GetTypes()).Should().NotContain(t => typeof(JobPlatform.SharedKernel.Messaging.IDomainEventMapper).IsAssignableFrom(t));
    }
}
