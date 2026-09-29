using JobPlatform.JobPosting.Application.DTOs.SavedSearches;
using JobPlatform.JobPosting.Domain;

namespace JobPlatform.JobPosting.Application.Commands.SavedSearches;

public sealed record SaveSearchCommand(
    string? Keyword, string? Governorate, string? City, decimal? SalaryMin, decimal? SalaryMax, string? ContractType, DateTime? PostedAfterUtc,
    DateTime? DeadlineBeforeUtc, string? CategoryCode, bool NotifyOnMatch) : JobSeekerCommand<SavedSearchView>
{
    public SearchCriteria ToDomain() => new(Keyword, Governorate, City, SalaryMin, SalaryMax,
        ContractType is null ? null : Enum.Parse<Domain.ContractType>(ContractType, true), PostedAfterUtc, DeadlineBeforeUtc, CategoryCode);
}
