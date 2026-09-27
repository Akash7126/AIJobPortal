using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.GovernmentIntegration.Application;
using JobPlatform.GovernmentIntegration.Domain;
using JobPlatform.GovernmentIntegration.Infrastructure.Adapters;
using JobPlatform.GovernmentIntegration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.GovernmentIntegration.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.GovernmentIntegration;

    /// <summary>Wires persistence, repositories, the read store, the anti-corruption adapters, the outbox publisher and the AccountCreated
    /// inbox consumer that feeds the KnownAccounts replica (handover section 5.2).</summary>
    public static IServiceCollection AddGovernmentIntegrationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<GovernmentIntegrationDbContext>(configuration, "Government", GovernmentIntegrationDbContext.Schema);
        services.AddDurableIdempotency<GovernmentIntegrationDbContext>();

        services.AddScoped<IEmployerVerificationRepository, EmployerVerificationRepository>();
        services.AddScoped<IGovernmentVerificationDataRepository, GovernmentVerificationDataRepository>();
        services.AddScoped<IEducationalCredentialVerificationRepository, EducationalCredentialVerificationRepository>();
        services.AddScoped<IIdentityVerificationRepository, IdentityVerificationRepository>();
        services.AddScoped<ILegacyDataRepository, LegacyDataRepository>();
        services.AddScoped<IDataQualityRepository, DataQualityRepository>();
        services.AddScoped<IMigrationRunRepository, MigrationRunRepository>();
        services.AddScoped<IGovernmentSourceConnectionRepository, GovernmentSourceConnectionRepository>();
        services.AddScoped<IKnownAccountRepository, KnownAccountRepository>();
        services.AddScoped<IGovernmentDataAccessLogRepository, GovernmentDataAccessLogRepository>();
        services.AddScoped<IGovernmentIntegrationReadStore, GovernmentIntegrationReadStore>();

        services.AddSingleton<IMolRegistryClient, StubMolRegistryClient>();
        services.AddSingleton<IPefClient, StubPefClient>();
        services.AddSingleton<IGovernmentDatabaseClient, StubGovernmentDatabaseClient>();
        services.AddSingleton<IEducationalInstitutionClient, StubEducationalInstitutionClient>();
        services.AddSingleton<IGovernmentIdClient, StubGovernmentIdClient>();
        services.AddSingleton<ILegacySourceReader, StubLegacySourceReader>();

        services.AddOutboxProcessor<GovernmentIntegrationDbContext>(configuration);
        services.AddInboxProcessor<GovernmentIntegrationDbContext>(configuration);
        services.AddInboxHandler<AccountCreatedIntegrationEvent, RecordKnownAccountHandler>(ConsumerName, "AccountCreated");

        return services;
    }
}
