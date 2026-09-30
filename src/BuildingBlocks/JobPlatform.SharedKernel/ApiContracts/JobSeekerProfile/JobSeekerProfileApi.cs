namespace JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;

/// <param name="Standing">"Active" or "Deactivated".</param>
/// <param name="EducationLevel">Highest degree: None, Primary, Secondary, Diploma, Bachelor, Master or Doctorate (null = not provided).</param>
/// <param name="PreferredWorkArrangements">OnSite, Remote, Hybrid (empty = no preference).</param>
/// <param name="Version">Profile aggregate version (used to make scoring idempotent per profile version).</param>
public sealed record ProfileForMatchingDto(
    Guid ProfileId, Guid OwnerAccountId, string Standing, long Version, string? Headline, IReadOnlyList<string> Skills, string? EducationLevel,
    IReadOnlyList<string> Training, string? Governorate, string? City, IReadOnlyList<string> PreferredWorkArrangements, decimal? YearsOfExperience,
    decimal? ExpectedSalaryMin, decimal? ExpectedSalaryMax);

/// <param name="Format">PDF, DOCX or TXT.</param>
public sealed record ResumeContentUrlDto(Guid ResumeId, Guid ProfileId, string Url, string Format, long SizeBytes, string Sha256, DateTime ExpiresAtUtc);

/// <param name="Visibility">"Public" or "Private".</param>
/// <param name="EmployerVisibilityOptIn">A private candidate who explicitly opted in to employer visibility (BC-11 Q-07; proposed BC-04 setting).</param>
/// <param name="DisclosedFields">Names of the fields the candidate discloses to employers (skills, education, experience, location, salary, availability, ...).</param>
public sealed record CandidatePrivacyDto(
    Guid ProfileId, string Visibility, bool EmployerVisibilityOptIn, bool Deactivated, IReadOnlyList<string> DisclosedFields, DateTime UpdatedAtUtc);

/// <summary>Searchable, privacy-filtered projection of a candidate. Only disclosed fields are populated; undisclosed ones are null/empty.</summary>
public sealed record CandidateViewDto(
    Guid ProfileId, string Visibility, bool EmployerVisibilityOptIn, bool Deactivated, IReadOnlyList<string> DisclosedFields,
    IReadOnlyList<string> Skills, string? EducationLevel, decimal? YearsOfExperience, string? LocationCode, IReadOnlyList<string> Languages,
    decimal? ExpectedSalaryMin, decimal? ExpectedSalaryMax, string? Availability, DateTime UpdatedAtUtc);
