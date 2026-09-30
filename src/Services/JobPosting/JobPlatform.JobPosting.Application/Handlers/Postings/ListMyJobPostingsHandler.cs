using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.JobPosting.Application.Interfaces;
using JobPlatform.JobPosting.Application.Queries.Postings;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobPosting.Application.Handlers.Postings;

internal sealed class ListMyJobPostingsHandler : IQueryHandler<ListMyJobPostingsQuery, PagedResult<JobPostingSummaryView>>
{
    private readonly IJobPostingSearchReadModel _search;
    private readonly ICurrentUser _user;

    public ListMyJobPostingsHandler(IJobPostingSearchReadModel search, ICurrentUser user)
    {
        _search = search;
        _user = user;
    }

    public async Task<Result<PagedResult<JobPostingSummaryView>>> Handle(ListMyJobPostingsQuery request, CancellationToken ct) =>
        await _search.ListByEmployerAsync(_user.UserId!.Value, request.Status, new PageRequest(request.Page, request.PageSize), ct);
}
