using JobPlatform.SharedKernel.ApiContracts.AiMatching;

namespace JobPlatform.AiMatching.Application.DTOs.Matching;

public sealed record MatchScoreDetailDto(
    Guid? MatchScoreId, Guid JobPostingId, string? Title, decimal Score, bool MeetsThreshold, int ConfigVersion, bool Stored, DateTime ComputedAtUtc,
    IReadOnlyList<MatchCriterionDto> Breakdown);
