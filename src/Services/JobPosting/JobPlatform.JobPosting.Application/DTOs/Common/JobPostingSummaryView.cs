namespace JobPlatform.JobPosting.Application.DTOs.Common;

public sealed record JobPostingSummaryView(
    Guid JobPostingId, LocalizedView Title, string CategoryCode, JobLocationView? Location, SalaryRangeView? Salary, DateTime DeadlineUtc, string Status,
    string ContractType, DateTime? PublishedAtUtc, decimal? RelevanceScore);
