namespace JobPlatform.SharedKernel.ApiContracts.AiMatching;

/// <param name="Criterion">SkillOverlap, Education, Training, Location, Experience or Salary.</param>
/// <param name="Score">0-100 (criterion score; meaningless when <paramref name="Included"/> is false).</param>
/// <param name="Weight">The weight used (0-100, the configured weight; the effective weight is re-normalised over the included criteria).</param>
/// <param name="Included">False when the profile or posting could not supply the criterion (zero-weight, excluded from the score).</param>
public sealed record MatchCriterionDto(string Criterion, decimal Score, decimal Weight, bool Included);

public sealed record MatchScoreDto(
    Guid MatchScoreId, Guid ProfileId, Guid JobPostingId, decimal Score, string ConfigVersion, DateTime ComputedAtUtc,
    IReadOnlyList<MatchCriterionDto> Breakdown);

/// <summary>Page of stored scores of one posting. <paramref name="PlatformThresholdPercent"/> is the platform Match Threshold (BC-10 setting) so a consumer can derive its own effective threshold.</summary>
public sealed record MatchScoreListDto(
    IReadOnlyList<MatchScoreDto> Items, int Page, int PageSize, int TotalCount, decimal PlatformThresholdPercent, string ConfigVersion);

public sealed record MatchRankingItemDto(Guid JobPostingId, string Title, decimal Score, DateTime ComputedAtUtc);

public sealed record MatchRankingDto(IReadOnlyList<MatchRankingItemDto> Items, int Page, int PageSize, int TotalCount, decimal PlatformThresholdPercent);

public sealed record ParsedFieldDto(string Name, string Value, decimal Confidence, bool NeedsReview);

public sealed record SkillMappingDto(string ExtractedTerm, string? CanonicalCode, decimal Confidence, bool IsFreeText, bool NeedsReview);

public sealed record ResumeParsedDataDto(
    Guid ResumeParsedDataId, Guid ResumeId, Guid ProfileId, string Language, string Status, string? FailureCode,
    IReadOnlyList<ParsedFieldDto> Fields, IReadOnlyList<SkillMappingDto> Skills);
