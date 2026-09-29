namespace JobPlatform.AiMatching.Application.DTOs.Matching;

public sealed record MatchedJobDto(Guid MatchScoreId, Guid JobPostingId, string Title, decimal Score, DateTime ComputedAtUtc);
