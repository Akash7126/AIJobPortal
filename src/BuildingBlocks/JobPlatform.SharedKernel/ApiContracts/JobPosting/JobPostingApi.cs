namespace JobPlatform.SharedKernel.ApiContracts.JobPosting;

/// <summary>
/// Synchronous contract of BC-09 Job Posting for other BCs (routes under /internal/v1, service token with scope identity.internal).
/// Consumers: BC-10 (postings/{id}) and BC-11 (postings/{id}: ownership and active state).
/// </summary>
public interface IJobPostingApi
{
    /// <summary>GET /internal/v1/postings/{id}. Null = 404.</summary>
    Task<PostingForMatchingDto?> GetPostingForMatchingAsync(Guid jobPostingId, CancellationToken ct = default);
}

/// <param name="Status">Lifecycle status of the posting; only "Active" postings are matched.</param>
/// <param name="EducationLevel">Required level: None, Primary, Secondary, Diploma, Bachelor, Master or Doctorate (null = none required).</param>
/// <param name="WorkArrangement">OnSite, Remote or Hybrid.</param>
public sealed record PostingForMatchingDto(
    Guid JobPostingId, Guid EmployerAccountId, string Status, bool Suspended, long Version, string Title, string Category, string? Description,
    IReadOnlyList<string> Skills, string? EducationLevel, IReadOnlyList<string> Training, string? Governorate, string? City, string WorkArrangement,
    int? MinExperienceYears, int? MaxExperienceYears, decimal? SalaryMin, decimal? SalaryMax);
