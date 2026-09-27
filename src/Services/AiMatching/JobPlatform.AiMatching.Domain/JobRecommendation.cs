using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

/// <summary>A posting with its content-based match score (0-100) and how well it fits the seeker's location/salary/arrangement preferences (0-1).</summary>
public sealed record ScoredPosting(Guid JobPostingId, decimal Score, decimal PreferenceFit);

public sealed record RecommendedJob(Guid JobPostingId, decimal Score, RecommendationReason Reason);

public sealed record JobRecommendationComputedDomainEvent(
    DateTime OccurredOnUtc, Guid JobRecommendationId, Guid ProfileId, Guid ActorId, IReadOnlyList<Guid> TopJobIds, RecommendationStrategy Strategy)
    : DomainEvent(OccurredOnUtc);

/// <summary>
/// Personalised job recommendations for one job seeker (handover 3.7). Hybrid (collaborative + content-based) when activity history exists; INV-15 with no or
/// low activity history it is content-based only. Results below the match threshold are filtered out. Regenerated wholesale on each run.
/// </summary>
public sealed class JobRecommendation : AggregateRoot<Guid>
{
    public const int DefaultMaxItems = 50;

    /// <summary>Weight of the content-based score in the hybrid blend; the collaborative signal gets the rest (proposed, A-01).</summary>
    public const decimal ContentWeight = 0.7m;

    /// <summary>Preference fit at or above this marks the recommendation reason as Preference.</summary>
    public const decimal PreferenceReasonFit = 0.99m;

    private JobRecommendation()
    {
    }

    public Guid ProfileId { get; private set; }
    public IReadOnlyList<RecommendedJob> Items { get; private set; } = Array.Empty<RecommendedJob>();
    public RecommendationStrategy Strategy { get; private set; }
    public DateTime ComputedAtUtc { get; private set; }

    /// <param name="collaborative">Collaborative score (0-100) per posting from similar seekers' activity. Empty = no usable history (INV-15).</param>
    public static JobRecommendation Compute(Guid profileId, IReadOnlyList<ScoredPosting> candidates, IReadOnlyDictionary<Guid, decimal> collaborative,
        decimal thresholdPercent, int maxItems, Guid actorId, DateTime nowUtc)
    {
        Guard.Ensure(maxItems > 0, AiRuleCodes.InvalidInput, "At least one recommendation must be allowed.");
        var strategy = collaborative.Count == 0 ? RecommendationStrategy.ContentOnly : RecommendationStrategy.Hybrid;

        var items = candidates
            .Select(c =>
            {
                var collab = collaborative.TryGetValue(c.JobPostingId, out var value) ? value : 0m;
                var score = strategy == RecommendationStrategy.ContentOnly ? c.Score : Math.Round(ContentWeight * c.Score + (1 - ContentWeight) * collab, 2);
                var reason = strategy == RecommendationStrategy.Hybrid && collab > c.Score ? RecommendationReason.Collaborative
                    : c.PreferenceFit >= PreferenceReasonFit ? RecommendationReason.Preference
                    : RecommendationReason.Content;
                return new RecommendedJob(c.JobPostingId, score, reason);
            })
            .Where(i => i.Score >= thresholdPercent)
            .OrderByDescending(i => i.Score).ThenBy(i => i.JobPostingId)
            .Take(maxItems)
            .ToArray();

        var recommendation = new JobRecommendation { Id = Guid.NewGuid(), ProfileId = profileId, Items = items, Strategy = strategy, ComputedAtUtc = nowUtc };
        recommendation.Raise(new JobRecommendationComputedDomainEvent(nowUtc, recommendation.Id, profileId, actorId, items.Take(10).Select(i => i.JobPostingId).ToArray(), strategy));
        return recommendation;
    }
}
