namespace JobPlatform.SharedKernel.ApiContracts.JobSeekerProfile;

/// <summary>
/// Synchronous contract of BC-04 Job Seeker Profile for other BCs (routes under /internal/v1, service token with scope identity.internal).
/// Consumers: BC-10 (profiles/{id}, resumes/{id}/content-url) and BC-11 (profiles/{id}/privacy, profiles/{id}/candidate-view).
/// Shapes are derived from the BC-04 handover section 6.1 (sync-read US-3.3.1-01, US-3.3.3-05) and the BC-11 handover section 6.2.
/// </summary>
public interface IJobSeekerProfileApi
{
    /// <summary>GET /internal/v1/profiles/{id} - skills, education, experience, preferences for scoring. Null = 404.</summary>
    Task<ProfileForMatchingDto?> GetProfileForMatchingAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>GET /internal/v1/resumes/{id}/content-url - short-lived signed URL of the resume file. Null = 404.</summary>
    Task<ResumeContentUrlDto?> GetResumeContentUrlAsync(Guid resumeId, CancellationToken ct = default);

    /// <summary>GET /internal/v1/profiles/{id}/privacy - candidate visibility. Null = 404.</summary>
    Task<CandidatePrivacyDto?> GetCandidatePrivacyAsync(Guid profileId, CancellationToken ct = default);

    /// <summary>GET /internal/v1/profiles/{id}/candidate-view - the fields visible under the candidate's current privacy settings. Null = 404.</summary>
    Task<CandidateViewDto?> GetCandidateViewAsync(Guid profileId, CancellationToken ct = default);
}

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
