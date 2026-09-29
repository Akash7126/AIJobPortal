namespace JobPlatform.CandidateSourcing.Application.DTOs.Recommendations;

public sealed record CandidateRecommendationItemView(Guid CandidateProfileId, decimal Score);
