namespace JobPlatform.AiMatching.Application.DTOs.Configuration;

public sealed record MatchingConfigurationDto(
    int Version, decimal MatchThresholdPercent, WeightsDto Weights, int ShortlistSize, decimal LowConfidenceThresholdPercent, DateTime UpdatedAtUtc, string ETag);
