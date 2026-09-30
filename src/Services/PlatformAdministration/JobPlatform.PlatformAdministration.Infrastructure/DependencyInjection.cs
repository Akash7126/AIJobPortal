using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Http;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.PlatformAdministration.Application.Events;
using JobPlatform.PlatformAdministration.Application.Interfaces;
using JobPlatform.PlatformAdministration.Domain.Interfaces.Repositories;
using JobPlatform.PlatformAdministration.Infrastructure.Adapters;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence;
using JobPlatform.PlatformAdministration.Infrastructure.Persistence.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.PlatformAdministration.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.PlatformAdministration;

    /// <summary>
    /// Wires persistence, repositories, the read store, reference-data cache, seeding, the outbox publisher, the inbox consumer of JobPostingCreated and the
    /// adapters to other BCs. Adapters are selected by configuration: UserDirectory:Provider and ReferenceUsage:Provider = Http | Fake.
    /// </summary>
    public static IServiceCollection AddPlatformAdministrationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<AdminDbContext>(configuration, "Admin", AdminDbContext.Schema);
        services.AddDurableIdempotency<AdminDbContext>();
        services.AddDbSeeder<AdminDbContext, AdminSeeder>();

        services.AddScoped<IPlatformEntityRecordRepository, PlatformEntityRecordRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<IReferenceFileRepository, ReferenceFileRepository>();
        services.AddScoped<IPlatformTaxonomyRepository, PlatformTaxonomyRepository>();
        services.AddScoped<IJobOfferingRepository, JobOfferingRepository>();
        services.AddScoped<IAdminReadStore, AdminReadStore>();
        services.AddSingleton<IReferenceDataCache, ReferenceDataCache>();

        AddLaterSaveWins(services);

        services.AddOutboxProcessor<AdminDbContext>(configuration);
        services.AddInboxProcessor<AdminDbContext>(configuration);
        services.AddInboxConsumer<JobPostingCreatedIntegrationEvent, RegisterJobOfferingHandler>(ConsumerName);

        AddUserDirectory(services, configuration);
        AddReferenceUsage(services, configuration);
        return services;
    }

    /// <summary>Later-save-wins retry must wrap the unit of work, so it is inserted in front of it in the behavior pipeline.</summary>
    private static void AddLaterSaveWins(IServiceCollection services)
    {
        var descriptor = ServiceDescriptor.Scoped(typeof(IPipelineBehavior<,>), typeof(LaterSaveWinsBehavior<,>));
        var unitOfWork = services.ToList().FindIndex(d => d.ServiceType == typeof(IPipelineBehavior<,>) && d.ImplementationType?.Name.StartsWith("UnitOfWorkBehavior", StringComparison.Ordinal) == true);
        if (unitOfWork >= 0)
        {
            services.Insert(unitOfWork, descriptor);
        }
        else
        {
            services.Add(descriptor);
        }
    }

    private static void AddUserDirectory(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<FakeUserDirectory>();
        services.AddHttpClient<HttpUserDirectory>((sp, client) =>
        {
            var baseUrl = sp.GetRequiredService<IConfiguration>()["Downstream:AccountIdentity:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            }

            client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IConfiguration>().GetValue("Downstream:TimeoutSeconds", 3));
        });
        services.AddScoped<IUserDirectory>(sp => IsProvider(sp, "UserDirectory:Provider", "Http")
            ? sp.GetRequiredService<HttpUserDirectory>()
            : sp.GetRequiredService<FakeUserDirectory>());
    }

    private static void AddReferenceUsage(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<InternalServiceTokenHandler>();
        services.AddHttpClient(InternalServiceTokenHandler.TokenClientName);
        services.AddHttpClient(HttpReferenceUsageChecker.ClientName, (sp, client) =>
                client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<IConfiguration>().GetValue("Downstream:TimeoutSeconds", 2)))
            .AddHttpMessageHandler<InternalServiceTokenHandler>();
        services.AddSingleton<HttpReferenceUsageChecker>();
        services.AddSingleton<FakeReferenceUsageChecker>();
        services.AddSingleton<IReferenceUsageChecker>(sp => IsProvider(sp, "ReferenceUsage:Provider", "Http")
            ? sp.GetRequiredService<HttpReferenceUsageChecker>()
            : sp.GetRequiredService<FakeReferenceUsageChecker>());
    }

    private static bool IsProvider(IServiceProvider sp, string key, string expected) =>
        string.Equals(sp.GetRequiredService<IConfiguration>()[key], expected, StringComparison.OrdinalIgnoreCase);
}
