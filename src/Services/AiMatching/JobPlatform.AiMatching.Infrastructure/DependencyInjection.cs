using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Infrastructure.Adapters;
using JobPlatform.AiMatching.Infrastructure.Ai;
using JobPlatform.AiMatching.Infrastructure.Clients;
using JobPlatform.AiMatching.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.ApiContracts.PlatformAdministration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AiMatching.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.AiMatching;

    /// <summary>Wires persistence, repositories, the AI adapters (deterministic local models by default), the consumed-API clients, the outbox (publishing), the inbox consumers and the workers.</summary>
    public static IServiceCollection AddAiMatchingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MatchingOptions>(configuration.GetSection(MatchingOptions.SectionName));
        services.AddSingleton<IPiiProtector>(sp => CreateProtector(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<IHostEnvironment>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("PiiProtector")));

        services.AddBcDbContext<AiMatchingDbContext>(configuration, "AiMatching", AiMatchingDbContext.Schema);
        services.AddDurableIdempotency<AiMatchingDbContext>();
        services.AddDbSeeder<AiMatchingDbContext, MatchingConfigurationSeeder>();

        services.AddScoped<IMatchingConfigurationRepository, MatchingConfigurationRepository>();
        services.AddScoped<IMatchScoreRepository, MatchScoreRepository>();
        services.AddScoped<IResumeParsedDataRepository, ResumeParsedDataRepository>();
        services.AddScoped<ISkillStandardizationRepository, SkillStandardizationRepository>();
        services.AddScoped<IParsedProfileDataRepository, ParsedProfileDataRepository>();
        services.AddScoped<IJobSemanticsRepository, JobSemanticsRepository>();
        services.AddScoped<IJobRecommendationRepository, JobRecommendationRepository>();
        services.AddScoped<ICandidateShortlistRepository, CandidateShortlistRepository>();
        services.AddScoped<IKnownProfileRepository, KnownProfileRepository>();
        services.AddScoped<IKnownPostingRepository, KnownPostingRepository>();
        services.AddScoped<IWorkItemRepository, WorkItemRepository>();
        services.AddScoped<IMatchReadStore, MatchReadStore>();
        services.AddScoped<IMatchingConfigurationProvider, MatchingConfigurationProvider>();
        services.AddScoped<ISkillTaxonomyProvider, SkillTaxonomyProvider>();

        // AI ports: deterministic local adapters (feature-hashing embeddings, rule-based parser and analyser, exact cosine search over stored vectors).
        // AiModels:* selects the adapter; only "Hashing"/"Rules"/"Keywords" ship, a model-server adapter is a drop-in behind the same ports.
        services.AddSingleton<IEmbeddingService, HashingEmbeddingService>();
        services.AddSingleton<IResumeParser, RuleBasedResumeParser>();
        services.AddSingleton<ISemanticAnalyzer, KeywordSemanticAnalyzer>();
        services.AddScoped<IVectorIndex, EfVectorIndex>();
        services.AddSingleton<IActivityHistory, NoActivityHistory>();

        // Consumed APIs: typed clients (contracts in SharedKernel) behind anti-corruption adapters.
        services.AddInternalApiClient<IJobSeekerProfileApi, JobSeekerProfileApiClient>("Downstream:JobSeekerProfile:BaseUrl");
        services.AddInternalApiClient<IJobPostingApi, JobPostingApiClient>("Downstream:JobPosting:BaseUrl");
        services.AddInternalApiClient<IPlatformAdministrationApi, PlatformAdministrationApiClient>("Downstream:PlatformAdministration:BaseUrl");
        services.AddHttpClient(ResumeContentSource.ClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IProfileDirectory, ProfileDirectory>();
        services.AddScoped<IPostingDirectory, PostingDirectory>();
        services.AddScoped<IResumeContentSource, ResumeContentSource>();

        services.AddOutboxProcessor<AiMatchingDbContext>(configuration);
        services.AddInboxProcessor<AiMatchingDbContext>(configuration);
        AddConsumers(services);

        services.AddSingleton<MatchingWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<MatchingWorker>());
        services.AddSingleton<WeeklyRecommendationScheduler>();
        services.AddHostedService(sp => sp.GetRequiredService<WeeklyRecommendationScheduler>());
        return services;
    }

    private static void AddConsumers(IServiceCollection services)
    {
        services.AddInboxConsumer<ResumeCreatedIntegrationEvent, ResumeCreatedHandler>(ConsumerName);
        services.AddInboxConsumer<ProfileCreatedIntegrationEvent, ProfileCreatedHandler>(ConsumerName);
        services.AddInboxConsumer<ProfileUpdatedIntegrationEvent, ProfileUpdatedHandler>(ConsumerName);
        services.AddInboxConsumer<JobPostingCreatedIntegrationEvent, JobPostingCreatedHandler>(ConsumerName);
        services.AddInboxConsumer<JobPostingUpdatedIntegrationEvent, JobPostingUpdatedHandler>(ConsumerName);
        services.AddInboxConsumer<JobPostingStatusUpdatedIntegrationEvent, JobPostingStatusUpdatedHandler>(ConsumerName);
        services.AddInboxConsumer<JobOfferingSuspendedIntegrationEvent, JobOfferingSuspendedHandler>(ConsumerName);
        services.AddInboxConsumer<PlatformTaxonomyUpdatedIntegrationEvent, PlatformTaxonomyUpdatedHandler>(ConsumerName);
    }

    private static IPiiProtector CreateProtector(IConfiguration configuration, IHostEnvironment environment, ILogger logger)
    {
        var key = configuration["Security:DataKey"];
        if (!string.IsNullOrWhiteSpace(key))
        {
            return new AesGcmPiiProtector(Convert.FromBase64String(key));
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException("Security:DataKey (base64 of 32 random bytes) is required in Production to encrypt parsed resume content.");
        }

        logger.LogWarning("Security:DataKey is not set: parsed resume content is stored WITHOUT application-level encryption (non-production only).");
        return NullPiiProtector.Instance;
    }
}
