namespace JobPlatform.SharedKernel.ApiContracts.JobPosting;

/// <param name="Status">Lifecycle status of the posting; only "Active" postings are matched.</param>
/// <param name="EducationLevel">Required level: None, Primary, Secondary, Diploma, Bachelor, Master or Doctorate (null = none required).</param>
/// <param name="WorkArrangement">OnSite, Remote or Hybrid.</param>
public sealed record PostingForMatchingDto(
    Guid JobPostingId, Guid EmployerAccountId, string Status, bool Suspended, long Version, string Title, string Category, string? Description,
    IReadOnlyList<string> Skills, string? EducationLevel, IReadOnlyList<string> Training, string? Governorate, string? City, string WorkArrangement,
    int? MinExperienceYears, int? MaxExperienceYears, decimal? SalaryMin, decimal? SalaryMax);
