using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.Notification.Application;
using JobPlatform.Notification.Application.Composition;
using JobPlatform.Notification.Domain;
using JobPlatform.Notification.Infrastructure.Adapters;
using JobPlatform.Notification.Infrastructure.Persistence;
using JobPlatform.Notification.Infrastructure.Realtime;
using JobPlatform.Notification.Infrastructure.Scheduling;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.AiMatching;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.Notification.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.Notification;

    /// <summary>Persistence, repositories, provider and contact adapters, real-time push, outbox, the inbox consumers of handover 5.2 and the schedulers.</summary>
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<NotificationDbContext>(configuration, "Notification", NotificationDbContext.Schema);
        services.AddDurableIdempotency<NotificationDbContext>();
        services.AddDbSeeder<NotificationDbContext, NotificationSeeder>();

        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.Configure<NotificationSecurityOptions>(configuration.GetSection(NotificationSecurityOptions.SectionName));

        services.AddScoped<IInAppNotificationRepository, InAppNotificationRepository>();
        services.AddScoped<IOutboundMessageRepository, OutboundMessageRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<IEmailTemplateRepository, EmailTemplateRepository>();
        services.AddScoped<INotificationTypeRepository, NotificationTypeRepository>();
        services.AddScoped<ISmsPolicyRepository, SmsPolicyRepository>();
        services.AddScoped<IJobConfirmationRepository, JobConfirmationRepository>();
        services.AddScoped<IWeeklyCycleRepository, WeeklyCycleRepository>();
        services.AddScoped<INotificationReadStore, NotificationReadStore>();

        // Anti-corruption adapters: providers are deterministic fakes until real credentials exist; contacts come from BC-03 (Http) or a fake in dev/test.
        services.AddSingleton<FakeProviders>();
        services.AddSingleton<IEmailProvider>(sp => sp.GetRequiredService<FakeProviders>());
        services.AddSingleton<ISmsGateway>(sp => sp.GetRequiredService<FakeProviders>());
        services.AddSingleton<FakeAccountContactApi>();
        services.AddHttpClient();
        services.AddSingleton<HttpAccountContactApi>();
        services.AddSingleton<IAccountContactApi>(sp =>
            string.Equals(sp.GetRequiredService<IConfiguration>()["Contacts:Provider"], "Http", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<HttpAccountContactApi>()
                : sp.GetRequiredService<FakeAccountContactApi>());
        services.AddSingleton<IUnsubscribeTokens, HmacUnsubscribeTokens>();
        services.AddSingleton<IWebhookVerifier, HmacWebhookVerifier>();

        services.AddSignalR();
        services.AddSingleton<ConnectionTracker>();
        services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();

        services.AddOutboxProcessor<NotificationDbContext>(configuration);
        services.AddInboxProcessor<NotificationDbContext>(configuration);
        services.AddInboxConsumer<SavedSearchMatchedIntegrationEvent, NotifySavedSearchMatchHandler>(ConsumerName);
        services.AddInboxConsumer<JobRecommendationComputedIntegrationEvent, SendWeeklyRecommendationHandler>(ConsumerName);
        services.AddInboxConsumer<JobDataImportedIntegrationEvent, SendJobConfirmationHandler>(ConsumerName);
        services.AddInboxConsumer<AccountApprovedIntegrationEvent, SendWelcomeHandler>(ConsumerName);
        services.AddInboxConsumer<AccountSuspendedIntegrationEvent, SuppressRecipientHandler>(ConsumerName);

        services.AddSingleton<DispatcherService>();
        services.AddHostedService(sp => sp.GetRequiredService<DispatcherService>());
        services.AddSingleton<DigestService>();
        services.AddHostedService(sp => sp.GetRequiredService<DigestService>());
        return services;
    }
}
