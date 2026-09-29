using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.DTOs.Common;

/// <summary>Result of a job-posting search (foundation section 12: THR-013 &lt;= 2s).</summary>
public sealed record SearchCriteriaInput(
    string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax, string? ContractType, DateTime? PostedAfterUtc,
    DateTime? DeadlineBeforeUtc, string? CategoryCode)
{
    public SearchCriteria ToDomain() => new(Keyword, Governorate, City, SalaryMin, SalaryMax,
        ContractType is null ? null : Enum.Parse<Domain.ContractType>(ContractType, true), PostedAfterUtc, DeadlineBeforeUtc, CategoryCode);
}
