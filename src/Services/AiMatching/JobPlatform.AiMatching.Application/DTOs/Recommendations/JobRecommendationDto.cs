namespace JobPlatform.AiMatching.Application.DTOs.Recommendations;

public sealed record JobRecommendationDto(Guid Id, string Strategy, DateTime ComputedAtUtc, IReadOnlyList<RecommendedJobDto> Items);
