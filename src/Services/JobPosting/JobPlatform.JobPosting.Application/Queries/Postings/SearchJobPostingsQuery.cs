using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.JobPosting.Application.Queries.Postings;

public sealed record SearchJobPostingsQuery(
    string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax, string? ContractType, DateTime? PostedAfterUtc,
    DateTime? DeadlineBeforeUtc, string? CategoryCode, string? Sort, int Page = 1, int PageSize = 20) : PublicQuery<PagedResult<JobPostingSummaryView>>
{
    public SearchCriteriaInput ToCriteria() => new(Keyword, Governorate, City, SalaryMin, SalaryMax, ContractType, PostedAfterUtc, DeadlineBeforeUtc, CategoryCode);
}
