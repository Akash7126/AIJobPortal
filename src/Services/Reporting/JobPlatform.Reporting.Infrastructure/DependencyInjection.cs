using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.Reporting.Application;
using JobPlatform.Reporting.Application.DTOs.Activity;
using JobPlatform.Reporting.Application.Ingestion;
using JobPlatform.Reporting.Domain;
using JobPlatform.Reporting.Infrastructure.Adapters;
using JobPlatform.Reporting.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobPlatform.Reporting.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.Reporting;

    /// <summary>Wires persistence, repositories, the analytics store, the external-system adapters, the outbox (two proposed events plus the access audit record), the inbox
    /// consumers for the 45 catalogued events and the scheduled jobs.</summary>
    public static IServiceCollection AddReportingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReportingOptions>(configuration.GetSection(ReportingOptions.SectionName));
        services.Configure<JobOptions>(configuration.GetSection(JobOptions.SectionName));

        services.AddBcDbContext<ReportingDbContext>(configuration, "Reporting", ReportingDbContext.ConfigSchema);
        services.AddDurableIdempotency<ReportingDbContext>();

        services.AddScoped<IRetentionPolicyRepository, RetentionPolicyRepository>();
        services.AddScoped<IReportTemplateRepository, ReportTemplateRepository>();
        services.AddScoped<IReportScheduleRepository, ReportScheduleRepository>();
        services.AddScoped<ISavedReportRepository, SavedReportRepository>();
        services.AddScoped<IReportExportRepository, ReportExportRepository>();
        services.AddScoped<IReportAccessRuleRepository, ReportAccessRuleRepository>();
        services.AddScoped<IPerformanceAlertRuleRepository, PerformanceAlertRuleRepository>();
        services.AddScoped<ILaborMarketReportRepository, LaborMarketReportRepository>();
        services.AddScoped<IFactStore, FactStore>();
        services.AddScoped<IAnalyticsQueryService, AnalyticsQueryService>();

        AddAdapters(services, configuration);

        // Publishes the two proposed events and the access-decision audit records.
        services.AddOutboxProcessor<ReportingDbContext>(configuration);
        services.AddInboxProcessor<ReportingDbContext>(configuration);
        AddConsumers(services);

        services.AddSingleton<ReportingJobs>();
        services.AddHostedService<ReportingWorker>();
        return services;
    }

    private static void AddAdapters(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient();
        services.AddSingleton<SimulatedMetricsSource>();
        services.AddSingleton<PrometheusMetricsSource>();
        services.AddSingleton<NoMetricsSource>();
        services.AddSingleton<IMetricsSource>(sp => Provider(configuration, "Metrics:Provider", "Simulated") switch
        {
            "prometheus" => sp.GetRequiredService<PrometheusMetricsSource>(),
            "none" => sp.GetRequiredService<NoMetricsSource>(),
            _ => sp.GetRequiredService<SimulatedMetricsSource>()
        });

        services.AddSingleton<SimulatedSessionSource>();
        services.AddSingleton<HttpSessionSource>();
        services.AddSingleton<ISessionSource>(sp => Provider(configuration, "Sessions:Provider", "Simulated") switch
        {
            "http" => sp.GetRequiredService<HttpSessionSource>(),
            "none" => new NoSessionSource(),
            _ => sp.GetRequiredService<SimulatedSessionSource>()
        });

        services.AddSingleton<SimulatedPowerBiExporter>();
        services.AddSingleton<HttpPowerBiExporter>();
        services.AddSingleton<IPowerBiExporter>(sp => Provider(configuration, "PowerBi:Provider", "Simulated") switch
        {
            "http" => sp.GetRequiredService<HttpPowerBiExporter>(),
            _ => sp.GetRequiredService<SimulatedPowerBiExporter>()
        });
    }

    /// <summary>Provider names are read when the service is first resolved, so environment and test overrides always win.</summary>
    private static string Provider(IConfiguration configuration, string key, string fallback) => (configuration[key] ?? fallback).ToLowerInvariant();

    private static void AddConsumers(IServiceCollection services)
    {
        // One generic handler per event: dedupe, append the FactEvent and rollups, then run the event's projectors (handover 5.2).
        foreach (var eventType in EventCatalog.ConsumedEvents)
        {
            var register = typeof(DependencyInjection).GetMethod(nameof(AddConsumer), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(eventType);
            register.Invoke(null, new object[] { services });
        }
    }

    private static void AddConsumer<TEvent>(IServiceCollection services) where TEvent : IIntegrationEvent =>
        services.AddInboxConsumer<TEvent, IngestionHandler<TEvent>>(ConsumerName);
}

/// <summary>Session source switched off: the login dashboard reports the source as unavailable.</summary>
internal sealed class NoSessionSource : ISessionSource
{
    public Task<IReadOnlyList<ActiveSession>?> GetActiveAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ActiveSession>?>(null);
}
