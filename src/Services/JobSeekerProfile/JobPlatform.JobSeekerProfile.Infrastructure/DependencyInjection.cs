using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.JobSeekerProfile.Application.Inbox;
using JobPlatform.JobSeekerProfile.Application.Interfaces;
using JobPlatform.JobSeekerProfile.Application.ShareLink;
using JobPlatform.JobSeekerProfile.Domain.Interfaces.Repositories;
using JobPlatform.JobSeekerProfile.Infrastructure.Adapters;
using JobPlatform.JobSeekerProfile.Infrastructure.Persistence;
using JobPlatform.JobSeekerProfile.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.JobSeekerProfile.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.JobSeekerProfile;

    public static IServiceCollection AddJobSeekerProfileInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<JobSeekerProfileDbContext>(configuration, "JobSeekerProfile", JobSeekerProfileDbContext.Schema);
        services.AddDurableIdempotency<JobSeekerProfileDbContext>();
        services.Configure<PublicSiteOptions>(configuration.GetSection(PublicSiteOptions.SectionName));

        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<IResumeRepository, ResumeRepository>();
        services.AddScoped<IProfileShareLinkRepository, ProfileShareLinkRepository>();
        services.AddScoped<ISupplementaryDocumentRepository, SupplementaryDocumentRepository>();
        services.AddScoped<IJobPreferenceRepository, JobPreferenceRepository>();
        services.AddScoped<IPrivacySettingRepository, PrivacySettingRepository>();
        services.AddScoped<IKnownAccountRepository, KnownAccountRepository>();
        services.AddScoped<IProcessedParsedDataRepository, ProcessedParsedDataRepository>();
        services.AddScoped<IProfileReadStore, ProfileReadStore>();

        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IMalwareScanner, NoOpMalwareScanner>();
        services.AddSingleton<IQrCodeRenderer, PlaceholderQrCodeRenderer>();
        AddAccountIdentityClient(services, configuration);

        services.AddOutboxProcessor<JobSeekerProfileDbContext>(configuration);
        services.AddInboxProcessor<JobSeekerProfileDbContext>(configuration);
        services.AddInboxConsumer<AccountApprovedIntegrationEvent, RecordKnownAccountHandler>(ConsumerName);
        services.AddInboxConsumer<AccountSuspendedIntegrationEvent, DeactivateProfileHandler>(ConsumerName);
        services.AddInboxConsumer<ResumeParsedDataComputedIntegrationEvent, ApplyExtractedProfileDataHandler>(ConsumerName);
        services.AddInboxConsumer<ParsedProfileDataUpdatedIntegrationEvent, ApplyReviewedDataHandler>(ConsumerName);
        services.AddInboxConsumer<PlatformTaxonomyUpdatedIntegrationEvent, RefreshTaxonomyCacheHandler>(ConsumerName);

        return services;
    }

    private static void AddAccountIdentityClient(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<FakeAccountIdentityClient>();
        services.AddHttpClient<HttpAccountIdentityClient>(HttpAccountIdentityClient.ClientName, (sp, client) =>
        {
            var baseUrl = sp.GetRequiredService<IConfiguration>()["Downstream:AccountIdentity:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            }

            client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IConfiguration>().GetValue("Downstream:TimeoutSeconds", 2));
        });
        services.AddScoped<IAccountIdentityClient>(sp =>
            string.Equals(sp.GetRequiredService<IConfiguration>()["AccountIdentity:Provider"], "Http", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<HttpAccountIdentityClient>()
                : sp.GetRequiredService<FakeAccountIdentityClient>());
    }
}
