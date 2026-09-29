using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.JobPosting.Application.Queries.InternalApi;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.InternalApi;

internal sealed class ListOpenPostingsByEmployerHandler : IQueryHandler<ListOpenPostingsByEmployerQuery, PagedResult<JobPostingSummaryView>>
{
    private readonly IJobPostingSearchReadModel _search;

    public ListOpenPostingsByEmployerHandler(IJobPostingSearchReadModel search) => _search = search;

    public async Task<Result<PagedResult<JobPostingSummaryView>>> Handle(ListOpenPostingsByEmployerQuery request, CancellationToken ct) =>
        await _search.ListOpenByEmployerAsync(request.EmployerAccountId, new PageRequest(request.Page, request.PageSize), ct);
}
