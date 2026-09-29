using JobPlatform.JobPosting.Application.DTOs.Common;

namespace JobPlatform.JobPosting.Application.DTOs.Postings;

public sealed record JobPostingView(
    Guid JobPostingId, Guid? EmployerAccountId, string Source, LocalizedView Title, LocalizedView Summary, IReadOnlyList<string> Skills,
    string CategoryCode, string ContractType, string? EducationLevel, IReadOnlyList<string> RequiredTraining, string WorkFormat,
    JobLocationView? Location, SalaryRangeView? Salary, int? MinExperienceYears, int? MaxExperienceYears, IReadOnlyList<string> RequiredLanguages,
    DateTime DeadlineUtc, bool AutoClose, string? JobLink, IReadOnlyDictionary<string, string> OtherFields, JobVisibilityView Visibility,
    string Status, bool AdminSuspended, int TaxonomyVersion, DateTime CreatedAtUtc, DateTime? PublishedAtUtc, DateTime UpdatedAtUtc, byte[] RowVersion);
