using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobPosting.Application;
using JobPlatform.JobPosting.Application.Events;
using JobPlatform.JobPosting.Domain;
using JobPlatform.JobPosting.Infrastructure.Adapters;
using JobPlatform.JobPosting.Infrastructure.Persistence;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.JobPosting.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.JobPosting;

    /// <summary>
    /// Wires persistence, repositories, the search read model, the outbox publisher, the inbox consumers of JobDataImported/JobPostAttributionUpdated
    /// (BC-02), JobOfferingSuspended/PlatformTaxonomyUpdated (BC-08), the taxonomy/employer-standing/match-ranking clients (adapters chosen by
    /// &lt;Port&gt;:Provider = Http | Fake) and the auto-close background job.
    /// </summary>
    public static IServiceCollection AddJobPostingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<JobPostingDbContext>(configuration, "JobPosting", JobPostingDbContext.Schema);
        services.AddDurableIdempotency<JobPostingDbContext>();

        services.AddScoped<IJobPostingRepository, JobPostingRepository>();
        services.AddScoped<IFavoriteJobListRepository, FavoriteJobListRepository>();
        services.AddScoped<ISavedSearchRepository, SavedSearchRepository>();
        services.AddScoped<IInterestedListRepository, InterestedListRepository>();
        services.AddScoped<IJobPostingSearchReadModel, JobPostingSearchReadModel>();
        services.AddSingleton<IJobPostingCache, JobPostingCache>();
        services.AddScoped<IJobPostingSchemaValidator, JobPostingSchemaValidator>();

        services.AddOutboxProcessor<JobPostingDbContext>(configuration);
        services.AddInboxProcessor<JobPostingDbContext>(configuration);
        services.AddInboxConsumer<JobDataImportedIntegrationEvent, ImportExternalJobHandler>(ConsumerName);
        services.AddInboxConsumer<JobPostAttributionUpdatedIntegrationEvent, SyncExternalPostingHandler>(ConsumerName);
        services.AddInboxConsumer<JobOfferingSuspendedIntegrationEvent, ApplyAdminSuspensionHandler>(ConsumerName);
        services.AddInboxConsumer<PlatformTaxonomyUpdatedIntegrationEvent, RefreshTaxonomyCacheHandler>(ConsumerName);

        AddTaxonomyProvider(services);
        AddEmployerStandingProvider(services);
        AddMatchRankingProvider(services);

        services.Configure<JobOptions>(configuration.GetSection(JobOptions.SectionName));
        services.AddHostedService<ExpireDuePostingsJob>();
        return services;
    }

    private static void AddTaxonomyProvider(IServiceCollection services)
    {
        services.AddSingleton<FakeTaxonomyProvider>();
        services.AddInternalApiClient<HttpTaxonomyProvider, HttpTaxonomyProvider>("Downstream:PlatformAdministration:BaseUrl");
        services.AddScoped<ITaxonomyProvider>(sp => IsProvider(sp, "Taxonomy:Provider", "Http")
            ? sp.GetRequiredService<HttpTaxonomyProvider>()
            : sp.GetRequiredService<FakeTaxonomyProvider>());
    }

    private static void AddEmployerStandingProvider(IServiceCollection services)
    {
        services.AddSingleton<FakeEmployerStandingProvider>();
        services.AddInternalApiClient<HttpEmployerStandingProvider, HttpEmployerStandingProvider>("Downstream:EmployerOnboarding:BaseUrl");
        services.AddScoped<IEmployerStandingProvider>(sp => IsProvider(sp, "EmployerStanding:Provider", "Http")
            ? sp.GetRequiredService<HttpEmployerStandingProvider>()
            : sp.GetRequiredService<FakeEmployerStandingProvider>());
    }

    private static void AddMatchRankingProvider(IServiceCollection services)
    {
        services.AddSingleton<FakeMatchRankingProvider>();
        services.AddInternalApiClient<HttpMatchRankingProvider, HttpMatchRankingProvider>("Downstream:AiMatching:BaseUrl");
        services.AddScoped<IMatchRankingProvider>(sp => IsProvider(sp, "MatchRanking:Provider", "Http")
            ? sp.GetRequiredService<HttpMatchRankingProvider>()
            : sp.GetRequiredService<FakeMatchRankingProvider>());
    }

    private static bool IsProvider(IServiceProvider sp, string key, string expected) =>
        string.Equals(sp.GetRequiredService<IConfiguration>()[key], expected, StringComparison.OrdinalIgnoreCase);
}
