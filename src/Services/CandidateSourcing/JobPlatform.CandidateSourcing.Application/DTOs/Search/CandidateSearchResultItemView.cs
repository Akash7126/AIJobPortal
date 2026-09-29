namespace JobPlatform.CandidateSourcing.Application.DTOs.Search;

public sealed record CandidateSearchResultItemView(
    Guid CandidateProfileId, IReadOnlyList<string> Skills, string? EducationLevel, decimal? YearsOfExperience, string? LocationCode,
    decimal? SalaryMin, decimal? SalaryMax);
