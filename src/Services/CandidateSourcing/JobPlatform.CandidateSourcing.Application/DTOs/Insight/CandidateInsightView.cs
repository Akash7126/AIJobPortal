namespace JobPlatform.CandidateSourcing.Application.DTOs.Insight;

public sealed record CandidateInsightView(
    Guid CandidateInsightId, Guid CandidateProfileId, Guid JobPostingId, string? Availability, decimal? ExpectedSalaryMin, decimal? ExpectedSalaryMax,
    decimal OverallScore, IReadOnlyList<string> Strengths, IReadOnlyList<string> Gaps, IReadOnlyList<string> WithheldFields, DateTime ComputedAtUtc);
