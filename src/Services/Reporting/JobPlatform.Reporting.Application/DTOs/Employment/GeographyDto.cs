namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record GeographyDto(DateOnly From, DateOnly To, IReadOnlyList<GeographyRowDto> Jobs, IReadOnlyList<GeographyRowDto> Candidates);
