namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record SalaryRowDto(string Group, long? Count, decimal? Min, decimal? Average, decimal? Max, bool Suppressed);
