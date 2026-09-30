using System.Reflection;
using JobPlatform.CandidateSourcing.Api;
using JobPlatform.CandidateSourcing.Application;
using JobPlatform.CandidateSourcing.Domain.Common;
using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.CandidateSourcing.Infrastructure;
using JobPlatform.TestSupport;

namespace JobPlatform.CandidateSourcing.ArchitectureTests;

/// <summary>Structural rules of foundation sections 4 and 14.1 for BC-11 (shared rules live in JobPlatform.TestSupport).</summary>
public class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(ITalentPoolEntryRepository).Assembly;
    private static readonly Assembly Application = typeof(ApplicationAssembly).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(CandidateSourcingErrorMessages).Assembly;

    private const string Bc = "CandidateSourcing";

    [Fact]
    public void Domain_DependsOnNothingButTheSharedKernel() =>
        ArchitectureRules.DomainDependsOn(Domain, $"JobPlatform.{Bc}.Application", $"JobPlatform.{Bc}.Infrastructure", $"JobPlatform.{Bc}.Api", "JobPlatform.BuildingBlocks")
            .Should().BeEmpty();

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApiOrFrameworks() =>
        ArchitectureRules.DoesNotDependOn(Application, $"JobPlatform.{Bc}.Infrastructure", $"JobPlatform.{Bc}.Api", "Microsoft.EntityFrameworkCore", "RabbitMQ",
            "StackExchange.Redis", "Microsoft.AspNetCore", "JobPlatform.BuildingBlocks").Should().BeEmpty();

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        ArchitectureRules.DoesNotDependOn(Infrastructure, $"JobPlatform.{Bc}.Api").Should().BeEmpty();

    [Fact]
    public void NoBoundedContext_ReferencesAnotherOne() =>
        ArchitectureRules.ReferencedOtherBoundedContexts(Bc, Domain, Application, Infrastructure, Api).Should().BeEmpty();

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
    public void RequestHandlers_AreInternalSealed() => ArchitectureRules.HandlersNotInternalSealed(Application).Should().BeEmpty();

    [Fact]
    public void Aggregates_HaveNoPublicSetters() => ArchitectureRules.PublicSettersInDomain(Domain).Should().BeEmpty();

    [Fact]
    public void Controllers_DoNotUseTheDbContext() => ArchitectureRules.ControllersUsingPersistence(Api).Should().BeEmpty();

    [Fact]
    public void EveryPublishedErrorCode_HasAnEnglishAndAnArabicMessage()
    {
        var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!);

        foreach (var code in codes)
        {
            CandidateSourcingErrorMessages.Catalog.Should().ContainKey(code);
            var (en, ar) = CandidateSourcingErrorMessages.Catalog[code];
            en.Should().NotBeNullOrWhiteSpace($"{code} needs an English message");
            ar.Should().NotBeNullOrWhiteSpace($"{code} needs an Arabic message");
            ar.Any(c => c is >= '؀' and <= 'ۿ').Should().BeTrue($"{code}'s Arabic message must be Arabic");
        }
    }

    [Fact]
    public void ThisServicePublishesEventsThroughOneMapper()
    {
        var mappers = Application.GetTypes().Concat(Infrastructure.GetTypes())
            .Where(t => typeof(JobPlatform.SharedKernel.Messaging.Interfaces.IDomainEventMapper).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });
        mappers.Should().ContainSingle();
    }
}
