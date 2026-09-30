using JobPlatform.CandidateSourcing.Application.DTOs.Recommendations;
using JobPlatform.CandidateSourcing.Application.Queries.Recommendations;
using JobPlatform.CandidateSourcing.Application.Services.Recommendations;
using JobPlatform.CandidateSourcing.Domain.Insight;
using JobPlatform.CandidateSourcing.Domain.Ranking;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.Recommendations;

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
