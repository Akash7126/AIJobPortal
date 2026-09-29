namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record EmploymentOutcomesDto(DateOnly From, DateOnly To, IReadOnlyList<OutcomeItemDto> Items, int ExcludedWithoutFollowUp);
