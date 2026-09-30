using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.BuildingBlocks.Infrastructure.Caching;
using JobPlatform.BuildingBlocks.Infrastructure.Interfaces.Caching;
using Microsoft.Extensions.Logging;

namespace JobPlatform.AiMatching.Infrastructure.Adapters;

/// <summary>Cache-aside read of the matching configuration (key config:current, 10 min, evicted on change). A cache failure degrades to the database (foundation section 10).</summary>
internal sealed class MatchingConfigurationProvider(IMatchingConfigurationRepository repository, ICacheStore cache, ILogger<MatchingConfigurationProvider> logger)
    : IMatchingConfigurationProvider
{
    public const string Key = "config:current";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private sealed record Entry(int Version, decimal Threshold, decimal[] Weights, int ShortlistSize, decimal LowConfidence);

    public async Task<MatchingConfigSnapshot> GetAsync(CancellationToken ct = default)
    {
        try
        {
            if (await cache.GetJsonAsync<Entry>(Key, ct) is { Weights.Length: 6 } hit)
            {
                return ToSnapshot(hit);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Configuration cache read failed; reading the database");
        }

        var configuration = await repository.GetCurrentAsync(ct);
        if (configuration is null)
        {
            return MatchingConfigSnapshot.Defaults;
        }

        var snapshot = configuration.Snapshot();
        try
        {
            var w = snapshot.Weights;
            await cache.SetJsonAsync(Key, new Entry(snapshot.ConfigVersion, snapshot.MatchThresholdPercent,
                new[] { w.SkillOverlap, w.Education, w.Training, w.Location, w.Experience, w.Salary }, snapshot.ShortlistSize, snapshot.LowConfidenceThresholdPercent), Ttl, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Configuration cache write failed");
        }

        return snapshot;
    }

    public async Task InvalidateAsync(CancellationToken ct = default) => await cache.RemoveAsync(Key, ct);

    private static MatchingConfigSnapshot ToSnapshot(Entry e) =>
        new(e.Version, e.Threshold, CriterionWeights.Create(e.Weights[0], e.Weights[1], e.Weights[2], e.Weights[3], e.Weights[4], e.Weights[5]), e.ShortlistSize, e.LowConfidence);
}
