using JobPlatform.AiMatching.Domain;

namespace JobPlatform.AiMatching.Application.DTOs.Semantics;

/// <summary>A posting as BC-09 publishes it for matching, translated into this BC's language.</summary>
public sealed record PostingSource(
    Guid JobPostingId, Guid EmployerAccountId, string Status, bool Suspended, long Version, string Title, string Category, string? Description,
    IReadOnlyList<string> Skills, EducationLevel? EducationLevel, IReadOnlyList<string> Training, string? Governorate, string? City, WorkArrangement Arrangement,
    int? MinExperienceYears, int? MaxExperienceYears, decimal? SalaryMin, decimal? SalaryMax)
{
    public bool IsActive => !Suspended && string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);
}
