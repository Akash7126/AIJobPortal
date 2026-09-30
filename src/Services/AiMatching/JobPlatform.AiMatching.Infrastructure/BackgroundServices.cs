using JobPlatform.AiMatching.Application;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.AiMatching.Infrastructure.Persistence;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobPlatform.AiMatching.Infrastructure;

/// <summary>Seeds the singleton matching configuration (defaults: threshold 60, shortlist 100, low confidence 70). Idempotent.</summary>
internal sealed class MatchingConfigurationSeeder(TimeProvider clock) : IDbSeeder<AiMatchingDbContext>
{
    public async Task SeedAsync(AiMatchingDbContext db, CancellationToken ct)
    {
        if (!await db.MatchingConfigurations.AnyAsync(c => c.Id == MatchingConfiguration.SingletonId, ct))
        {
            db.MatchingConfigurations.Add(MatchingConfiguration.CreateDefault(clock.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync(ct);
        }
    }
}

/// <summary>Polls the work queue and runs due items (batch shortlist, fan-out matching, embedding refresh, re-standardisation, recommendations).</summary>
public sealed class MatchingWorker(IServiceScopeFactory scopes, IOptions<MatchingOptions> options, TimeProvider clock, ILogger<MatchingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await RunOnceAsync(stoppingToken) == 0)
                {
                    await Task.Delay(options.Value.WorkPollInterval, clock, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Matching worker loop failed; retrying");
                await Task.Delay(options.Value.WorkPollInterval, clock, stoppingToken);
            }
        }
    }

    /// <summary>One pass over the due items; exposed so tests can drive the worker deterministically.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MatchingWorkRunner>().RunOnceAsync(ct);
    }
}

/// <summary>
/// Weekly job-recommendation run (handover 3.7): enqueues a recommendation computation per active profile. A cache lock (lock:weekly-recs) makes exactly one
/// replica run it (foundation section 10, approved case: scheduled-job leader election). BC-13 delivers the weekly mail from JobRecommendationComputed.
/// </summary>
public sealed class WeeklyRecommendationScheduler(IServiceScopeFactory scopes, ICacheStore cache, IOptions<MatchingOptions> options, TimeProvider clock,
    ILogger<WeeklyRecommendationScheduler> logger) : BackgroundService
{
    private const string LockKey = "lock:weekly-recs";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WeeklyRecommendationsEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(options.Value.WeeklyRecommendationsInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Weekly recommendation scheduling failed; retrying in one hour");
                await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
            }
        }
    }

    /// <summary>Queues one recommendation per active profile. Returns the number queued (0 when another replica holds the lock).</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        if (!await cache.SetIfNotExistsAsync(LockKey, Environment.MachineName, TimeSpan.FromMinutes(30), ct))
        {
            return 0;
        }

        try
        {
            var queued = 0;
            for (var skip = 0; ; skip += 500)
            {
                using var scope = scopes.CreateScope();
                var profiles = await scope.ServiceProvider.GetRequiredService<IKnownProfileRepository>().ListActiveAsync(skip, 500, ct);
                var work = scope.ServiceProvider.GetRequiredService<IWorkItemRepository>();
                foreach (var profile in profiles)
                {
                    if (await work.EnqueueAsync(WorkItemKind.ComputeRecommendation, profile.Id, clock.GetUtcNow().UtcDateTime, ct))
                    {
                        queued++;
                    }
                }

                await scope.ServiceProvider.GetRequiredService<AiMatchingDbContext>().SaveChangesAsync(ct);
                if (profiles.Count < 500)
                {
                    return queued;
                }
            }
        }
        finally
        {
            await cache.RemoveAsync(LockKey, CancellationToken.None);
        }
    }
}
