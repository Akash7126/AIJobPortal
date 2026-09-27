using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.CandidateSourcing.Application;
using JobPlatform.CandidateSourcing.Application.Events;
using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.CandidateSourcing.Infrastructure.Adapters;
using JobPlatform.CandidateSourcing.Infrastructure.Persistence;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.CandidateSourcing.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.CandidateSourcing;

    /// <summary>
    /// Wires persistence, repositories, the read store, the cache-aside store, the outbox publisher, the four inbox consumers (handover 5.2) and
    /// the dual-provider clients to BC-09/BC-04/BC-10 (Http | Fake, selected per client by configuration).
    /// </summary>
    public static IServiceCollection AddCandidateSourcingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<CandidateSourcingDbContext>(configuration, "CandidateSourcing", CandidateSourcingDbContext.Schema);
        services.AddDurableIdempotency<CandidateSourcingDbContext>();

        services.AddScoped<ITalentPoolEntryRepository, TalentPoolEntryRepository>();
        services.AddScoped<IQualificationThresholdRepository, QualificationThresholdRepository>();
        services.AddScoped<ICandidateInsightRepository, CandidateInsightRepository>();
        services.AddScoped<ICandidateProjectionRepository, CandidateProjectionRepository>();
        services.AddScoped<IVerifiedEmployerRepository, VerifiedEmployerRepository>();
        services.AddScoped<ICandidateSourcingReadStore, CandidateSourcingReadStore>();
        services.AddSingleton<ICandidateSourcingCache, CandidateSourcingCache>();

        services.AddOutboxProcessor<CandidateSourcingDbContext>(configuration);
        services.AddInboxProcessor<CandidateSourcingDbContext>(configuration);
        services.AddInboxConsumer<MatchScoreComputedIntegrationEvent, MarkPostingMatchesStaleHandler>(ConsumerName);
        services.AddInboxConsumer<ProfileCreatedIntegrationEvent, ProfileCreatedProjectionHandler>(ConsumerName);
        services.AddInboxConsumer<ProfileUpdatedIntegrationEvent, ProfileUpdatedProjectionHandler>(ConsumerName);
        services.AddInboxConsumer<AccountSuspendedIntegrationEvent, ExcludeCandidateHandler>(ConsumerName);
        services.AddInboxConsumer<EmployerVerificationApprovedIntegrationEvent, MarkEmployerVerifiedHandler>(ConsumerName);

        services.AddTransient<InternalServiceTokenHandler>();
        services.AddTransient<RetryIdempotentGetHandler>();
        services.AddHttpClient(InternalServiceTokenHandler.TokenClientName);

        AddJobPostingClient(services, configuration);
        AddJobSeekerProfileClient(services, configuration);
        AddAiMatchingClient(services, configuration);
        return services;
    }

    private static void AddJobPostingClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<HttpJobPostingApiClient>((sp, client) => ConfigureDownstream(sp, client, "Downstream:JobPosting:BaseUrl"))
            .AddHttpMessageHandler<RetryIdempotentGetHandler>().AddHttpMessageHandler<InternalServiceTokenHandler>();
        services.AddSingleton<FakeJobPostingApiClient>();
        services.AddScoped<IJobPostingApi>(sp => IsProvider(sp, "JobPostingClient:Provider")
            ? sp.GetRequiredService<HttpJobPostingApiClient>()
            : sp.GetRequiredService<FakeJobPostingApiClient>());
    }

    private static void AddJobSeekerProfileClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<HttpJobSeekerProfileApiClient>((sp, client) => ConfigureDownstream(sp, client, "Downstream:JobSeekerProfile:BaseUrl"))
            .AddHttpMessageHandler<RetryIdempotentGetHandler>().AddHttpMessageHandler<InternalServiceTokenHandler>();
        services.AddSingleton<FakeJobSeekerProfileApiClient>();
        services.AddScoped<IJobSeekerProfileApi>(sp => IsProvider(sp, "JobSeekerProfileClient:Provider")
            ? sp.GetRequiredService<HttpJobSeekerProfileApiClient>()
            : sp.GetRequiredService<FakeJobSeekerProfileApiClient>());
    }

    private static void AddAiMatchingClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<HttpAiMatchingApiClient>((sp, client) => ConfigureDownstream(sp, client, "Downstream:AiMatching:BaseUrl"))
            .AddHttpMessageHandler<RetryIdempotentGetHandler>().AddHttpMessageHandler<InternalServiceTokenHandler>();
        services.AddSingleton<FakeAiMatchingApiClient>();
        services.AddScoped<IAiMatchingApi>(sp => IsProvider(sp, "AiMatchingClient:Provider")
            ? sp.GetRequiredService<HttpAiMatchingApiClient>()
            : sp.GetRequiredService<FakeAiMatchingApiClient>());
    }

    private static void ConfigureDownstream(IServiceProvider sp, HttpClient client, string baseUrlKey)
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var baseUrl = configuration[baseUrlKey];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }

        client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("Downstream:TimeoutSeconds", 2));
    }

    private static bool IsProvider(IServiceProvider sp, string key) =>
        string.Equals(sp.GetRequiredService<IConfiguration>()[key], "Http", StringComparison.OrdinalIgnoreCase);
}
