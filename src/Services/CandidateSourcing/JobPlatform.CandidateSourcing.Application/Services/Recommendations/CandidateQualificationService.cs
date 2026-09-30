using JobPlatform.CandidateSourcing.Application.Interfaces;
using JobPlatform.CandidateSourcing.Domain.Interfaces.Repositories;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Ranking;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.Interfaces.JobPosting;

namespace JobPlatform.CandidateSourcing.Application.Services.Recommendations;

/// <summary>
/// Shared by the recommendation and ranking queries: verifies the posting is owned by the caller, snapshots the effective threshold once
/// (INV-06) and filters the scored candidates by current privacy (US-3.3.3-05). Caches the qualifying list per posting (evicted by
/// MarkPostingMatchesStaleHandler on MatchScoreComputed).
/// </summary>
internal sealed class CandidateQualificationService
{
    private readonly IJobPostingApi _postings;
    private readonly IAiMatchingApi _matching;
    private readonly IQualificationThresholdRepository _thresholds;
    private readonly ICandidateProjectionRepository _projections;
    private readonly ICandidateSourcingCache _cache;

    public CandidateQualificationService(IJobPostingApi postings, IAiMatchingApi matching, IQualificationThresholdRepository thresholds,
        ICandidateProjectionRepository projections, ICandidateSourcingCache cache)
    {
        _postings = postings;
        _matching = matching;
        _thresholds = thresholds;
        _projections = projections;
        _cache = cache;
    }

    public async Task<Qualification?> ForPostingAsync(Guid jobPostingId, Guid employerId, CancellationToken ct)
    {
        var posting = await _postings.GetPostingForMatchingAsync(jobPostingId, ct);
        if (posting is null)
        {
            return null;
        }

        if (posting.EmployerAccountId != employerId)
        {
            return new Qualification(true, Array.Empty<(Guid, decimal, MatchCriterionDto[])>());
        }

        var cacheKey = CacheKeys.Recommendations(jobPostingId);
        var scored = await _cache.GetAsync<CachedScore[]>(cacheKey, ct);
        if (scored is null)
        {
            var employerThreshold = await _thresholds.GetAsync(employerId, jobPostingId, ct);
            var scores = await _matching.ListMatchScoresAsync(jobPostingId, null, 1, 200, ct);
            var effective = EffectiveThresholdCalculator.Effective(scores.PlatformThresholdPercent, employerThreshold?.Percent ?? 0);

            var qualifiers = new List<CachedScore>();
            foreach (var score in scores.Items.Where(s => s.Score >= effective))
            {
                var projection = await _projections.GetAsync(score.ProfileId, ct);
                var snapshot = projection?.ToSnapshot(Array.Empty<string>());
                if (snapshot is null || !CandidatePrivacyPolicy.IsVisibleToEmployers(snapshot))
                {
                    continue;
                }

                qualifiers.Add(new CachedScore(score.ProfileId, score.Score, score.Breakdown.ToArray()));
            }

            scored = qualifiers.ToArray();
            await _cache.SetAsync(cacheKey, scored, CacheKeys.RecommendationsTtl, ct);
        }

        return new Qualification(false, scored.Select(s => (s.ProfileId, s.Score, s.Breakdown)).ToArray());
    }

    private sealed record CachedScore(Guid ProfileId, decimal Score, MatchCriterionDto[] Breakdown);
}
