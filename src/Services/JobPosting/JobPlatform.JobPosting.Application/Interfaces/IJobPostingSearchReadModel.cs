using JobPlatform.JobPosting.Application.DTOs.Common;
using JobPlatform.JobPosting.Application.DTOs.Postings;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.JobPosting.Application.Interfaces;

/// <summary>Read/search side (foundation section 3.5): dedicated projections over EF, never the aggregate. Backed by SQL Server full-text search
/// in production; a LIKE-based fallback on SQLite (dev/tests) — see the Infrastructure implementation and the BC-09 status doc.</summary>
public interface IJobPostingSearchReadModel
{
    Task<PagedResult<JobPostingSummaryView>> SearchAsync(SearchCriteriaInput criteria, string? sort, PageRequest page, CancellationToken ct = default);

    Task<JobPostingView?> GetAsync(Guid id, CancellationToken ct = default);

    Task<PagedResult<JobPostingSummaryView>> ListByEmployerAsync(Guid employerAccountId, string? status, PageRequest page, CancellationToken ct = default);

    Task<PagedResult<JobPostingSummaryView>> ListOpenByEmployerAsync(Guid employerAccountId, PageRequest page, CancellationToken ct = default);

    /// <summary>Which of the given codes of this reference type still appear on a non-archived posting (US-3.1.4-07, /internal/v1/reference-usage/check).</summary>
    Task<IReadOnlyList<string>> CheckReferenceUsageAsync(string type, IReadOnlyCollection<string> codes, CancellationToken ct = default);

    Task<JobPostingSchemaView> GetSchemaAsync(CancellationToken ct = default);
}
