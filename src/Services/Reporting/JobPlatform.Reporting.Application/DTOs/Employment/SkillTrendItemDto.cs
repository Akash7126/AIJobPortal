namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record SkillTrendItemDto(string Skill, long Demand, long PreviousDemand, decimal? Growth, long Supply, long Gap, bool Emerging);
