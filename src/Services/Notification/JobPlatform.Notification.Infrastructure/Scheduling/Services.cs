using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.Notification.Application;
using JobPlatform.Notification.Application.Commands.Delivery;
using JobPlatform.Notification.Application.Delivery;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.Notification.Infrastructure.Scheduling;

/// <summary>Polls for due e-mail and SMS and drives them through the provider with retry (30 s timeout, 3 retries). Tests call <see cref="RunOnceAsync"/> directly.</summary>
public sealed class DispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<NotificationOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<DispatcherService> _logger;

    public DispatcherService(IServiceScopeFactory scopes, IOptions<NotificationOptions> options, TimeProvider clock, ILogger<DispatcherService> logger)
    {
        _scopes = scopes;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.DispatcherEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunOnceAsync(stoppingToken) == 0)
                {
                    await Task.Delay(_options.Value.DispatchInterval, _clock, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dispatch loop failed; retrying");
                await Task.Delay(_options.Value.DispatchInterval, _clock, stoppingToken);
            }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<OutboundDispatcher>().RunOnceAsync(_options.Value.DispatchBatchSize, ct);
    }
}

/// <summary>Builds the daily digest. A cache lock keeps one replica in charge (foundation section 10, lock:digest).</summary>
public sealed class DigestService : BackgroundService
{
    private const string LockKey = "notification:lock:digest";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<NotificationOptions> _options;
    private readonly ICacheStore _cache;
    private readonly TimeProvider _clock;
    private readonly ILogger<DigestService> _logger;

    public DigestService(IServiceScopeFactory scopes, IOptions<NotificationOptions> options, ICacheStore cache, TimeProvider clock, ILogger<DigestService> logger)
    {
        _scopes = scopes;
        _options = options;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.DigestEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.Value.DigestInterval, _clock, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Digest run failed; retrying at the next interval");
            }
        }
    }

    /// <summary>One digest pass over everything created up to now. Returns the number of digests built (0 when another instance holds the lock).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        if (!await _cache.SetIfNotExistsAsync(LockKey, Environment.MachineName, TimeSpan.FromMinutes(10), ct))
        {
            return 0;
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var command = new BuildDailyDigestCommand(_clock.GetUtcNow().UtcDateTime);
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(command, ct);
            return result.IsSuccess ? result.Value : 0;
        }
        finally
        {
            await _cache.RemoveAsync(LockKey, CancellationToken.None);
        }
    }
}
