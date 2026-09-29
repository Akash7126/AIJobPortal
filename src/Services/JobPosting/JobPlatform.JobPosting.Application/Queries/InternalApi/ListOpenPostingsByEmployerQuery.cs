using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.JobPosting.Application.Queries.InternalApi;

public sealed record ListOpenPostingsByEmployerQuery(Guid EmployerAccountId, int Page = 1, int PageSize = 20) : ServiceQuery<PagedResult<JobPostingSummaryView>>;
