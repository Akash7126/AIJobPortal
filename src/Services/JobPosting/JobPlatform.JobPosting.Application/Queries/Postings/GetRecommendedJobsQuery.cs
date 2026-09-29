using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.JobPosting.Application.Queries.Postings;

public sealed record GetRecommendedJobsQuery(int Page = 1, int PageSize = 20) : JobSeekerQuery<PagedResult<JobPostingSummaryView>>;
