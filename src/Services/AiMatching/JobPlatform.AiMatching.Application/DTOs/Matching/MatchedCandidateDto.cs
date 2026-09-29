namespace JobPlatform.AiMatching.Application.DTOs.Matching;

public sealed record MatchedCandidateDto(Guid MatchScoreId, Guid ProfileId, decimal Score, DateTime ComputedAtUtc);
