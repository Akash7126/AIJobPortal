namespace JobPlatform.AiMatching.Application.DTOs.Recommendations;

public sealed record RecommendedJobDto(Guid JobPostingId, string Title, decimal Score, string Reason);
