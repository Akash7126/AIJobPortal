using JobPlatform.Reporting.Application.DTOs.Common;

namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record EmploymentMetricsDto(DateOnly From, DateOnly To, string Granularity, bool InsufficientData, IReadOnlyList<TrendPointDto> PostingTrend, FigureDto ApplicationRate,
    FigureDto HiringRate, FigureDto TimeToClose);
