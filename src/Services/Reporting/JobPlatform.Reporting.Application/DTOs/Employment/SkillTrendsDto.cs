namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record SkillTrendsDto(DateOnly From, DateOnly To, string Granularity, bool InsufficientHistory, IReadOnlyList<SkillTrendItemDto> Items);
