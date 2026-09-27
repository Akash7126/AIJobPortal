namespace JobPlatform.CandidateSourcing.Domain.Ranking;

public sealed record RankedCandidate(Guid CandidateProfileId, decimal Score, int Rank);

/// <summary>CS.Ranking.TIE_BREAK_LOWEST_ID (US-3.3.3-02 AC-02): order by score descending; equal scores are ordered by the lowest candidate id, so the
/// result is stable across repeated calls even when the underlying scores tie.</summary>
public static class CandidateRankingService
{
    public static IReadOnlyList<RankedCandidate> Rank(IEnumerable<(Guid CandidateProfileId, decimal Score)> candidates)
    {
        var ordered = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.CandidateProfileId)
            .ToArray();

        var ranked = new RankedCandidate[ordered.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            ranked[i] = new RankedCandidate(ordered[i].CandidateProfileId, ordered[i].Score, i + 1);
        }

        return ranked;
    }
}

/// <summary>A-01: the threshold actually applied is the higher of the platform-wide Match Threshold (BC-10) and the employer's own qualification threshold.</summary>
public static class EffectiveThresholdCalculator
{
    public static decimal Effective(decimal platformThresholdPercent, int employerThresholdPercent) =>
        Math.Max(platformThresholdPercent, employerThresholdPercent);
}
