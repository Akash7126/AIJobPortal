namespace JobPlatform.AiMatching.Application.DTOs.Parsing;

public sealed record ParsedProfileFieldDto(string Name, string Value, string Source, decimal Confidence, bool NeedsReview);
