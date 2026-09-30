using JobPlatform.AuditLogging.Application;
using JobPlatform.AuditLogging.Application.Commands.Exports;
using JobPlatform.AuditLogging.Application.Exports;
using JobPlatform.AuditLogging.Application.Ingestion;
using JobPlatform.AuditLogging.Application.Interfaces;
using JobPlatform.AuditLogging.Domain.Interfaces.Repositories;
using JobPlatform.AuditLogging.Infrastructure.Persistence;
using JobPlatform.AuditLogging.Infrastructure.Persistence.Repositories;
using JobPlatform.AuditLogging.Infrastructure.Reports;
using JobPlatform.BuildingBlocks.Infrastructure.DependencyInjection;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Messaging;
using JobPlatform.BuildingBlocks.Infrastructure.Persistence;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.IntegrationEvents.AccountIdentity;
using JobPlatform.SharedKernel.IntegrationEvents.Audit;
using JobPlatform.SharedKernel.IntegrationEvents.CandidateSourcing;
using JobPlatform.SharedKernel.IntegrationEvents.EmployerOnboarding;
using JobPlatform.SharedKernel.IntegrationEvents.ExternalIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.GovernmentIntegration;
using JobPlatform.SharedKernel.IntegrationEvents.JobPosting;
using JobPlatform.SharedKernel.IntegrationEvents.JobSeekerProfile;
using JobPlatform.SharedKernel.IntegrationEvents.Notification;
using JobPlatform.SharedKernel.IntegrationEvents.PlatformAdministration;
using JobPlatform.SharedKernel.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.AuditLogging.Infrastructure;

public static class DependencyInjection
{
    public const string ConsumerName = BoundedContextSlugs.AuditLogging;

    /// <summary>Wires persistence, repositories, the read store, the report adapter, the inbox consumers for the 19 catalogued events plus the audit-record stream, and the schedulers.</summary>
    public static IServiceCollection AddAuditLoggingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBcDbContext<AuditDbContext>(configuration, "Audit", AuditDbContext.Schema);
        services.AddDurableIdempotency<AuditDbContext>();

        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();
        services.AddScoped<ISyncJobStatusRepository, SyncJobStatusRepository>();
        services.AddScoped<IUsageCounterRepository, UsageCounterRepository>();
        services.AddScoped<IJobStatusHistoryRepository, JobStatusHistoryRepository>();
        services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        services.AddScoped<IEmployerDashboardRepository, EmployerDashboardRepository>();
        services.AddScoped<ICandidateInsightRepository, CandidateInsightRepository>();
        services.AddScoped<IExportJobRepository, ExportJobRepository>();
        services.AddScoped<IAuditReadStore, AuditReadStore>();

        services.Configure<AuditRetentionOptions>(configuration.GetSection(AuditRetentionOptions.SectionName));
        services.Configure<ExportOptions>(configuration.GetSection(ExportOptions.SectionName));
        services.AddSingleton<SimulatedReportGenerator>();
        services.AddScoped<IReportGenerator>(sp => sp.GetRequiredService<SimulatedReportGenerator>());

        // Read-model BC: nothing is published, so there is no outbox processor. It consumes only (inbox).
        services.AddInboxProcessor<AuditDbContext>(configuration);
        AddConsumers(services);

        services.AddSingleton<RetentionService>();
        services.AddHostedService(sp => sp.GetRequiredService<RetentionService>());
        services.AddSingleton<ExportGenerationService>();
        services.AddHostedService(sp => sp.GetRequiredService<ExportGenerationService>());
        return services;
    }

    private static void AddConsumers(IServiceCollection services)
    {
        // BC-03 Account Identity
        services.AddInboxConsumer<AccountCreatedIntegrationEvent, AccountCreatedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<AccountApprovedIntegrationEvent, AccountApprovedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<UserAccountApprovedIntegrationEvent, UserAccountApprovedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<ApiCredentialCreatedIntegrationEvent, ApiCredentialCreatedAuditHandler>(ConsumerName);
        // BC-04 / BC-05 / BC-01
        services.AddInboxConsumer<ProfileCreatedIntegrationEvent, ProfileCreatedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<EmployerRegistrationApprovedIntegrationEvent, EmployerRegistrationApprovedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<EmployerVerificationApprovedIntegrationEvent, EmployerVerificationApprovedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<GovernmentVerificationDataImportedIntegrationEvent, GovernmentVerificationDataImportedAuditHandler>(ConsumerName);
        // BC-02 External Integration
        services.AddInboxConsumer<JobDataImportedIntegrationEvent, JobDataImportedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<JobPostAttributionUpdatedIntegrationEvent, JobPostAttributionUpdatedAuditHandler>(ConsumerName);
        // BC-09 Job Posting (JobPostingCreated feeds the employer dashboard: handover Q-05)
        services.AddInboxConsumer<JobPostingCreatedIntegrationEvent, JobPostingCreatedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<JobPostingUpdatedIntegrationEvent, JobPostingUpdatedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<JobPostingStatusUpdatedIntegrationEvent, JobPostingStatusUpdatedAuditHandler>(ConsumerName);
        // BC-11 Candidate Sourcing
        services.AddInboxConsumer<CandidateInsightComputedIntegrationEvent, CandidateInsightComputedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<TalentPoolEntryCreatedIntegrationEvent, TalentPoolEntryCreatedAuditHandler>(ConsumerName);
        // BC-13 Notification
        services.AddInboxConsumer<NotificationSentIntegrationEvent, NotificationSentAuditHandler>(ConsumerName);
        services.AddInboxConsumer<NotificationStatusUpdatedIntegrationEvent, NotificationStatusUpdatedAuditHandler>(ConsumerName);
        // BC-08 Platform Administration
        services.AddInboxConsumer<PlatformEntityRecordCreatedIntegrationEvent, PlatformEntityRecordCreatedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<PlatformTaxonomyUpdatedIntegrationEvent, PlatformTaxonomyUpdatedAuditHandler>(ConsumerName);
        services.AddInboxConsumer<JobOfferingSuspendedIntegrationEvent, JobOfferingSuspendedAuditHandler>(ConsumerName);
        // Generic operational stream: one queue q.audit-logging.from.audit-records bound to every <source-bc>.<category>.v1 key of the exchange.
        services.AddInboxConsumer<AuditRecordIntegrationEvent, AuditRecordHandler>(ConsumerName, routingKeyOverride: "#", sourceSlugOverride: "audit-records");
    }
}

public sealed class ExportOptions
{
    public const string SectionName = "Exports";

    public bool Enabled { get; set; } = true;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public int BatchSize { get; set; } = 5;
}

/// <summary>Archives expired audit entries on a schedule. A cache lock ensures one instance runs the job when several replicas are deployed (foundation section 10).</summary>
public sealed class RetentionService : BackgroundService
{
    private const string LockKey = "audit-logging:lock:archive";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<AuditRetentionOptions> _options;
    private readonly ICacheStore _cache;
    private readonly TimeProvider _clock;
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(IServiceScopeFactory scopes, IOptions<AuditRetentionOptions> options, ICacheStore cache, TimeProvider clock, ILogger<RetentionService> logger)
    {
        _scopes = scopes;
        _options = options;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(_options.Value.Interval, _clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Archival run failed; retrying at the next interval");
                await Task.Delay(TimeSpan.FromMinutes(1), _clock, stoppingToken);
            }
        }
    }

    /// <summary>One archival pass (batches until nothing expired is left). Returns the number archived, or 0 when another instance holds the lock.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        if (!await _cache.SetIfNotExistsAsync(LockKey, Environment.MachineName, TimeSpan.FromMinutes(10), ct))
        {
            return 0;
        }

        try
        {
            var total = 0;
            int batch;
            do
            {
                using var scope = _scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ArchiveExpiredAuditEntriesCommand(_options.Value.BatchSize), ct);
                batch = result.IsSuccess ? result.Value : 0;
                total += batch;
            }
            while (batch >= _options.Value.BatchSize);

            if (total > 0)
            {
                _logger.LogInformation("Archived {Count} audit entries past their retention", total);
            }

            return total;
        }
        finally
        {
            await _cache.RemoveAsync(LockKey, CancellationToken.None);
        }
    }
}

/// <summary>Polls for queued administrator report exports and drives them to Ready or Failed (3.1.4-10).</summary>
public sealed class ExportGenerationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ExportOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExportGenerationService> _logger;

    public ExportGenerationService(IServiceScopeFactory scopes, IOptions<ExportOptions> options, TimeProvider clock, ILogger<ExportGenerationService> logger)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunOnceAsync(stoppingToken) == 0)
                {
                    await Task.Delay(_options.Value.PollInterval, _clock, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export generation loop failed; retrying");
                await Task.Delay(_options.Value.PollInterval, _clock, stoppingToken);
            }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ExportGenerationRunner>().RunOnceAsync(_options.Value.BatchSize, ct);
    }
}
