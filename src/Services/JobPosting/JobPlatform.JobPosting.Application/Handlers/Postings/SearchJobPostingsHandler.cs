using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.JobPosting.Application.Queries.Postings;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

internal sealed class SearchJobPostingsHandler : IQueryHandler<SearchJobPostingsQuery, PagedResult<JobPostingSummaryView>>
{
    private readonly IJobPostingSearchReadModel _search;

    public SearchJobPostingsHandler(IJobPostingSearchReadModel search) => _search = search;

    public async Task<Result<PagedResult<JobPostingSummaryView>>> Handle(SearchJobPostingsQuery request, CancellationToken ct) =>
        await _search.SearchAsync(request.ToCriteria(), request.Sort, new PageRequest(request.Page, request.PageSize), ct);
}
