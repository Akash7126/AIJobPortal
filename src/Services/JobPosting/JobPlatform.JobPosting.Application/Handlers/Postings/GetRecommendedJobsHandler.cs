using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.JobPosting.Application.Queries.Postings;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Ports;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

internal sealed class GetRecommendedJobsHandler : IQueryHandler<GetRecommendedJobsQuery, PagedResult<JobPostingSummaryView>>
{
    private readonly IMatchRankingProvider _ranking;
    private readonly IJobPostingSearchReadModel _search;
    private readonly ICurrentUser _user;

    public GetRecommendedJobsHandler(IMatchRankingProvider ranking, IJobPostingSearchReadModel search, ICurrentUser user)
    {
        _ranking = ranking;
        _search = search;
        _user = user;
    }

    public async Task<Result<PagedResult<JobPostingSummaryView>>> Handle(GetRecommendedJobsQuery request, CancellationToken ct)
    {
        var ranked = await _ranking.GetRankingAsync(_user.UserId!.Value, request.Page, request.PageSize, ct);
        if (ranked.Count == 0)
        {
            // Degrade to plain (unranked) search results when BC-10 is unavailable (handover section 6.2).
            return await _search.SearchAsync(new SearchCriteriaInput(null, null, null, null, null, null, null, null, null), "newest",
                new PageRequest(request.Page, request.PageSize), ct);
        }

        var items = new List<JobPostingSummaryView>();
        foreach (var item in ranked)
        {
            var view = await _search.GetAsync(item.JobPostingId, ct);
            if (view is not null)
            {
                items.Add(new JobPostingSummaryView(view.JobPostingId, view.Title, view.CategoryCode, view.Location, view.Salary, view.DeadlineUtc,
                    view.Status, view.ContractType, view.PublishedAtUtc, item.Score));
            }
        }

        return new PagedResult<JobPostingSummaryView>(items, request.Page, request.PageSize, items.Count);
    }
}
