using JobPlatform.CandidateSourcing.Application.DTOs.Recommendations;
using JobPlatform.CandidateSourcing.Application.Queries.Recommendations;
using JobPlatform.CandidateSourcing.Application.Services.Recommendations;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.CandidateSourcing.Application.Handlers.Recommendations;

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
