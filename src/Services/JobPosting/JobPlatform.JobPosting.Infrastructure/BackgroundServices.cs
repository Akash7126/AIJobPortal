using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.JobPosting.Application;
using JobPlatform.JobPosting.Application.Commands.Postings;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.JobPosting.Infrastructure;

public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    public bool Enabled { get; set; } = true;
    public TimeSpan ExpiryPollInterval { get; set; } = TimeSpan.FromMinutes(1);
    public int BatchSize { get; set; } = 200;
}

/// <summary>
/// US-3.2.1-01 AC-02 auto-close job: moves due postings with auto-close on to Expired. Only one replica runs it at a time (foundation section 10,
/// leader-election lock; not required by aggregate/outbox correctness - it just avoids duplicate work across replicas).
/// </summary>
public sealed class ExpireDuePostingsJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ICacheStore _cache;
    private readonly IOptions<JobOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExpireDuePostingsJob> _logger;

    public ExpireDuePostingsJob(IServiceScopeFactory scopes, ICacheStore cache, IOptions<JobOptions> options, TimeProvider clock,
        ILogger<ExpireDuePostingsJob> logger)
    {
        _scopes = scopes;
        _cache = cache;
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
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-close job failed; retrying at the next interval");
            }

            await Task.Delay(_options.Value.ExpiryPollInterval, _clock, stoppingToken);
        }
    }

    /// <summary>Public so tests can drive it deterministically without waiting for the interval.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        if (!await _cache.SetIfNotExistsAsync(CacheKeys.ExpiryJobLock, Environment.MachineName, TimeSpan.FromMinutes(5), ct))
        {
            return 0;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var command = new ExpireDuePostingsCommand(_options.Value.BatchSize);
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? result.Value : 0;
        }
        finally
        {
            await _cache.RemoveAsync(CacheKeys.ExpiryJobLock, CancellationToken.None);
        }
    }
}
