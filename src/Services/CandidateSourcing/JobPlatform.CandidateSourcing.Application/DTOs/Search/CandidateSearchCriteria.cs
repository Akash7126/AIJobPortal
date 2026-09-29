namespace JobPlatform.CandidateSourcing.Application.DTOs.Search;

/// <summary>US-3.3.3-04 GAP-001 (proposed set): skills, education, experience, location, salary, availability, language.</summary>
public sealed record CandidateSearchCriteria(
    IReadOnlyList<string>? Skills, string? EducationLevel, decimal? MinExperienceYears, decimal? MaxExperienceYears, string? LocationCode,
    decimal? SalaryMin, decimal? SalaryMax, string? Availability);
