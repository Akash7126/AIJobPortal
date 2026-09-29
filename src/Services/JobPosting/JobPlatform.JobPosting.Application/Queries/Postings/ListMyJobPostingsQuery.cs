using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.JobPosting.Application.Queries.Postings;

public sealed record ListMyJobPostingsQuery(string? Status, int Page = 1, int PageSize = 20) : EmployerQuery<PagedResult<JobPostingSummaryView>>;
