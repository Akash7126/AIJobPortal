using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.Reporting.Application;
using JobPlatform.Reporting.Application.Commands.Activity;
using JobPlatform.Reporting.Application.Commands.LaborMarket;
using JobPlatform.Reporting.Application.Commands.Performance;
using JobPlatform.Reporting.Application.Commands.ReportLibrary;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.Reporting.Infrastructure;

/// <summary>Scheduler settings (section "Jobs"). Intervals are per job; Enabled = false switches the whole worker off (tests drive the jobs directly).</summary>
public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    public bool Enabled { get; set; } = true;
    public TimeSpan ExportPollInterval { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan ScheduleInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan MetricsInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan AlertInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan RetentionInterval { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan LaborMarketInterval { get; set; } = TimeSpan.FromHours(6);
    public int BatchSize { get; set; } = 500;
}

/// <summary>
/// The scheduled jobs of the BC (handover 7): retention, report schedules, labor-market report, alert evaluation, metrics sampling and export generation.
/// The jobs that must run on one replica only take a cache lock (leader election, foundation section 10) for the duration of the run.
/// </summary>
public sealed class ReportingJobs
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ICacheStore _cache;
    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<ReportingJobs> _logger;

    public ReportingJobs(IServiceScopeFactory scopes, ICacheStore cache, IOptions<JobOptions> options, ILogger<ReportingJobs> logger)
    {
        _scopes = scopes;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public Task<int> GenerateExportsAsync(CancellationToken ct) => WithScopeAsync(async sp => await sp.GetRequiredService<ExportGenerationRunner>().RunOnceAsync(20, ct), ct);

    public Task<int> RunSchedulesAsync(CancellationToken ct) => LockedAsync("schedules", sp => sp.GetRequiredService<ScheduleRunner>().RunDueAsync(20, ct), ct);

    public Task<int> SampleMetricsAsync(CancellationToken ct) => LockedAsync("metrics", sp => Send(sp, new SampleSystemMetricsCommand(), ct), ct);

    public Task<int> EvaluateAlertsAsync(CancellationToken ct) => LockedAsync("alerts", sp => Send(sp, new EvaluatePerformanceAlertsCommand(), ct), ct);

    public Task<int> RunRetentionAsync(CancellationToken ct) => LockedAsync("retention", async sp =>
    {
        var total = 0;
        int batch;
        do
        {
            batch = await Send(sp, new RunRetentionJobCommand(_options.Value.BatchSize), ct);
            total += batch;
        }
        while (batch >= _options.Value.BatchSize);
        total += await Send(sp, new ArchiveExpiredSavedReportsCommand(_options.Value.BatchSize), ct);
        return total;
    }, ct);

    /// <summary>Generates the previous month's labor-market report when it does not exist yet (idempotent per period). Returns 1 when a report was created.</summary>
    public Task<int> GenerateLaborMarketReportAsync(CancellationToken ct) => LockedAsync("labor-market", async sp =>
    {
        var command = new RunScheduledLaborMarketReportCommand();
        var result = await sp.GetRequiredService<ISender>().Send(command, ct);
        return result.IsSuccess && !result.Value.Existing ? 1 : 0;
    }, ct);

    private static async Task<int> Send(IServiceProvider sp, ICommand<int> command, CancellationToken ct)
    {
        var result = await sp.GetRequiredService<ISender>().Send(command, ct);
        return result.IsSuccess ? result.Value : 0;
    }

    private async Task<int> WithScopeAsync(Func<IServiceProvider, Task<int>> job, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        return await job(scope.ServiceProvider);
    }

    private async Task<int> LockedAsync(string name, Func<IServiceProvider, Task<int>> job, CancellationToken ct)
    {
        var key = $"reporting:lock:{name}";
        if (!await _cache.SetIfNotExistsAsync(key, Environment.MachineName, TimeSpan.FromMinutes(10), ct))
        {
            _logger.LogDebug("Job {Job} is running on another instance", name);
            return 0;
        }

        try
        {
            return await WithScopeAsync(job, ct);
        }
        finally
        {
            await _cache.RemoveAsync(key, CancellationToken.None);
        }
    }
}

/// <summary>Runs the jobs at their configured intervals. Failures are logged and retried at the next tick; one failing job never stops the others.</summary>
public sealed class ReportingWorker : BackgroundService
{
    private readonly ReportingJobs _jobs;
    private readonly IOptions<JobOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ReportingWorker> _logger;

    public ReportingWorker(ReportingJobs jobs, IOptions<JobOptions> options, TimeProvider clock, ILogger<ReportingWorker> logger)
    {
        _jobs = jobs;
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

        var o = _options.Value;
        var schedule = new (string Name, TimeSpan Every, Func<CancellationToken, Task<int>> Run, DateTime[] Next)[]
        {
            ("exports", o.ExportPollInterval, _jobs.GenerateExportsAsync, new[] { DateTime.MinValue }),
            ("schedules", o.ScheduleInterval, _jobs.RunSchedulesAsync, new[] { DateTime.MinValue }),
            ("metrics", o.MetricsInterval, _jobs.SampleMetricsAsync, new[] { DateTime.MinValue }),
            ("alerts", o.AlertInterval, _jobs.EvaluateAlertsAsync, new[] { DateTime.MinValue }),
            ("retention", o.RetentionInterval, _jobs.RunRetentionAsync, new[] { DateTime.MinValue }),
            ("labor-market", o.LaborMarketInterval, _jobs.GenerateLaborMarketReportAsync, new[] { DateTime.MinValue })
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            foreach (var job in schedule.Where(j => j.Next[0] <= now))
            {
                job.Next[0] = now + job.Every;
                try
                {
                    await job.Run(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reporting job {Job} failed; it runs again at its next interval", job.Name);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), _clock, stoppingToken);
        }
    }
}
