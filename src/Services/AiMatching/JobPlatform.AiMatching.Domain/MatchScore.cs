using JobPlatform.AiMatching.Domain.Interfaces.Services;
using JobPlatform.SharedKernel.Domain;

namespace JobPlatform.AiMatching.Domain;

public sealed record MatchScoreComputedDomainEvent(
    DateTime OccurredOnUtc, Guid MatchScoreId, Guid ProfileId, Guid JobPostingId, decimal Score, int ConfigVersion) : DomainEvent(OccurredOnUtc);

/// <summary>
/// The 0-100 correspondence between one job seeker and one posting, latest value per (profile, posting) (handover 3.2).
/// INV-05 only active postings are scored; INV-06 recomputing with the same (config, profile, posting) versions changes nothing.
/// Only scores at or above (threshold - margin) are stored (D-01); the others are computed on demand.
/// </summary>
public sealed class MatchScore : AggregateRoot<Guid>
{
    private MatchScore()
    {
    }

    public Guid ProfileId { get; private set; }
    public Guid JobPostingId { get; private set; }
    public decimal Score { get; private set; }
    public IReadOnlyList<CriterionResult> Breakdown { get; private set; } = Array.Empty<CriterionResult>();
    public int ConfigVersion { get; private set; }
    public long ProfileVersion { get; private set; }
    public long PostingVersion { get; private set; }
    public string ModelVersion { get; private set; } = string.Empty;
    public DateTime ComputedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>Scores the pair with the configuration snapshot taken at the start of the run and raises <see cref="MatchScoreComputedDomainEvent"/>.</summary>
    public static MatchScore Compute(ProfileMatchView profile, PostingMatchView posting, MatchingConfigSnapshot config, ISkillSimilarity similarity,
        string modelVersion, DateTime nowUtc, DateTime? expiresAtUtc = null)
    {
        var computation = Evaluate(profile, posting, config, similarity);
        var score = new MatchScore { Id = Guid.NewGuid(), ProfileId = profile.ProfileId, JobPostingId = posting.PostingId };
        score.Apply(computation, profile, posting, config, modelVersion, nowUtc, expiresAtUtc);
        return score;
    }

    /// <summary>Scores without creating an aggregate (on-demand reads of pairs below the storage margin). Same rules: only active postings.</summary>
    public static MatchComputation Evaluate(ProfileMatchView profile, PostingMatchView posting, MatchingConfigSnapshot config, ISkillSimilarity similarity)
    {
        Guard.Ensure(posting.IsActive, AiRuleCodes.NonActivePosting, "Only active postings are scored.");
        return MatchScoringEngine.Instance.Compute(profile, posting, config.Weights, similarity);
    }

    /// <summary>True when the stored score was computed from exactly these versions (INV-06: nothing to do).</summary>
    public bool IsCurrent(MatchingConfigSnapshot config, long profileVersion, long postingVersion) =>
        ConfigVersion == config.ConfigVersion && ProfileVersion == profileVersion && PostingVersion == postingVersion;

    /// <summary>Recomputes in place. Returns false (and raises nothing) when the score is already current for these versions (INV-06).</summary>
    public bool Recompute(ProfileMatchView profile, PostingMatchView posting, MatchingConfigSnapshot config, ISkillSimilarity similarity, string modelVersion,
        DateTime nowUtc, DateTime? expiresAtUtc = null)
    {
        if (IsCurrent(config, profile.Version, posting.Version))
        {
            return false;
        }

        Apply(Evaluate(profile, posting, config, similarity), profile, posting, config, modelVersion, nowUtc, expiresAtUtc);
        return true;
    }

    private void Apply(MatchComputation computation, ProfileMatchView profile, PostingMatchView posting, MatchingConfigSnapshot config, string modelVersion,
        DateTime nowUtc, DateTime? expiresAtUtc)
    {
        Score = computation.Score;
        Breakdown = computation.Breakdown;
        ConfigVersion = config.ConfigVersion;
        ProfileVersion = profile.Version;
        PostingVersion = posting.Version;
        ModelVersion = modelVersion;
        ComputedAtUtc = nowUtc;
        ExpiresAtUtc = expiresAtUtc;
        Raise(new MatchScoreComputedDomainEvent(nowUtc, Id, ProfileId, JobPostingId, Score, config.ConfigVersion));
    }
}
