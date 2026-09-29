namespace JobPlatform.AiMatching.Application.DTOs.Parsing;

public sealed record ParsedProfileDataDto(
    Guid ParsedProfileDataId, Guid ResumeId, Guid ProfileId, string Status, decimal LowConfidenceThresholdPercent, IReadOnlyList<ParsedProfileFieldDto> Fields);
