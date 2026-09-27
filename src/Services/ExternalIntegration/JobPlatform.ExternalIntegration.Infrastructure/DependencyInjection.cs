using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.ExternalIntegration.Application;
using JobPlatform.ExternalIntegration.Application.Events;
using JobPlatform.ExternalIntegration.Domain;
using JobPlatform.ExternalIntegration.Infrastructure.Adapters;
using JobPlatform.ExternalIntegration.Infrastructure.Persistence;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.ExternalIntegration.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.ExternalIntegration;

    /// <summary>Wires persistence, repositories, the read store, the partner-feed adapter, the outbox publisher and the two inbox
    /// consumers of BC-03 (ApiCredentialCreated, AccountApproved).</summary>
    public static IServiceCollection AddExternalIntegrationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<ExternalIntegrationDbContext>(configuration, "External", ExternalIntegrationDbContext.Schema);
        services.AddDurableIdempotency<ExternalIntegrationDbContext>();

        services.AddScoped<IExternalJobSiteIntegrationRepository, ExternalJobSiteIntegrationRepository>();
        services.AddScoped<IJobDataRepository, JobDataRepository>();
        services.AddScoped<IJobPostAttributionRepository, JobPostAttributionRepository>();
        services.AddScoped<IJobDataMappingRepository, JobDataMappingRepository>();
        services.AddScoped<IApiVersionRepository, ApiVersionRepository>();
        services.AddScoped<ISoftwareInterfaceRepository, SoftwareInterfaceRepository>();
        services.AddScoped<IPartnerCredentialRepository, PartnerCredentialRepository>();
        services.AddScoped<IKnownPartnerAccountRepository, KnownPartnerAccountRepository>();
        services.AddScoped<IApiSchemaAccessLogRepository, ApiSchemaAccessLogRepository>();
        services.AddScoped<IExternalIntegrationReadStore, ExternalIntegrationReadStore>();

        services.AddSingleton<IPartnerJobFeedClient, FakePartnerJobFeedClient>();

        services.AddOutboxProcessor<ExternalIntegrationDbContext>(configuration);
        services.AddInboxProcessor<ExternalIntegrationDbContext>(configuration);
        services.AddInboxConsumer<ApiCredentialCreatedIntegrationEvent, RecordPartnerCredentialHandler>(ConsumerName);
        services.AddInboxConsumer<AccountApprovedIntegrationEvent, RegisterKnownPartnerAccountHandler>(ConsumerName);

        return services;
    }
}
