namespace JobPlatform.CandidateSourcing.Application.DTOs.Recommendations;

public sealed record CandidateRankingItemView(Guid CandidateProfileId, int Rank, decimal Score, IReadOnlyList<string> Strengths, IReadOnlyList<string> Gaps);
