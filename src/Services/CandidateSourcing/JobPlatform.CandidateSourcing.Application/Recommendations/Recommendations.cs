using JobPlatform.CandidateSourcing.Domain;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Privacy;
using JobPlatform.CandidateSourcing.Domain.Ranking;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.ApiContracts.JobPosting;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Recommendations;

internal readonly record struct Qualification(bool Forbidden, IReadOnlyList<(Guid ProfileId, decimal Score, MatchCriterionDto[] Breakdown)> Candidates);

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

/// <summary>US-3.3.3-01: candidates for one of the employer's active postings, scoring at or above the effective threshold. Empty is a normal
/// result (CS.Recommendation.EMPTY_IS_OK), never an error.</summary>
public sealed record GetCandidateRecommendationsQuery(Guid JobPostingId, int Page, int PageSize) : EmployerQuery<PagedResult<CandidateRecommendationItemView>>;

internal sealed class GetCandidateRecommendationsHandler : IQueryHandler<GetCandidateRecommendationsQuery, PagedResult<CandidateRecommendationItemView>>
{
    private readonly CandidateQualificationService _qualification;
    private readonly ICurrentUser _user;

    public GetCandidateRecommendationsHandler(CandidateQualificationService qualification, ICurrentUser user)
    {
        _qualification = qualification;
        _user = user;
    }

    public async Task<Result<PagedResult<CandidateRecommendationItemView>>> Handle(GetCandidateRecommendationsQuery request, CancellationToken ct)
    {
        var qualifying = await _qualification.ForPostingAsync(request.JobPostingId, ActorFactory.From(_user).Id, ct);
        if (qualifying is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The job posting was not found.");
        }

        if (qualifying.Value.Forbidden)
        {
            return Error.Forbidden(Domain.Common.ErrorCodes.Forbidden, "Only the owning employer may view this posting's candidates.");
        }

        var page = new PageRequest(request.Page, request.PageSize);
        var items = qualifying.Value.Candidates.Skip(page.Skip).Take(page.PageSize)
            .Select(c => new CandidateRecommendationItemView(c.ProfileId, c.Score)).ToArray();
        return new PagedResult<CandidateRecommendationItemView>(items, page.Page, page.PageSize, qualifying.Value.Candidates.Count);
    }
}

/// <summary>US-3.3.3-02: the recommendation with a stable rank position (CS.Ranking.TIE_BREAK_LOWEST_ID) and strengths/gaps highlighted.</summary>
public sealed record GetCandidateRankingQuery(Guid JobPostingId, int Page, int PageSize) : EmployerQuery<PagedResult<CandidateRankingItemView>>;

internal sealed class GetCandidateRankingHandler : IQueryHandler<GetCandidateRankingQuery, PagedResult<CandidateRankingItemView>>
{
    private readonly CandidateQualificationService _qualification;
    private readonly ICurrentUser _user;

    public GetCandidateRankingHandler(CandidateQualificationService qualification, ICurrentUser user)
    {
        _qualification = qualification;
        _user = user;
    }

    public async Task<Result<PagedResult<CandidateRankingItemView>>> Handle(GetCandidateRankingQuery request, CancellationToken ct)
    {
        var qualifying = await _qualification.ForPostingAsync(request.JobPostingId, ActorFactory.From(_user).Id, ct);
        if (qualifying is null)
        {
            return Error.NotFound(Domain.Common.ErrorCodes.NotFound, "The job posting was not found.");
        }

        if (qualifying.Value.Forbidden)
        {
            return Error.Forbidden(Domain.Common.ErrorCodes.Forbidden, "Only the owning employer may view this posting's candidates.");
        }

        var ranked = CandidateRankingService.Rank(qualifying.Value.Candidates.Select(c => (c.ProfileId, c.Score)));
        var byProfile = qualifying.Value.Candidates.ToDictionary(c => c.ProfileId);
        var page = new PageRequest(request.Page, request.PageSize);
        var items = ranked.Skip(page.Skip).Take(page.PageSize).Select(r =>
        {
            var breakdown = byProfile[r.CandidateProfileId].Breakdown;
            var fit = Fit.From(r.Score, breakdown.Where(b => b.Included).Select(b => (b.Criterion, b.Score)).ToArray());
            return new CandidateRankingItemView(r.CandidateProfileId, r.Rank, r.Score, fit.Strengths.ToArray(), fit.Gaps.ToArray());
        }).ToArray();
        return new PagedResult<CandidateRankingItemView>(items, page.Page, page.PageSize, ranked.Count);
    }
}
