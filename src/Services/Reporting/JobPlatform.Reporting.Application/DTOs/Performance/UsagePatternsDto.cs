using JobPlatform.Reporting.Application.DTOs.Common;

namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record UsagePatternsDto(DateOnly From, DateOnly To, IReadOnlyList<HourCountDto> PeakHours, IReadOnlyList<ActivityCountDto> PopularFeatures, long ActiveActors,
    decimal AverageDailyActiveActors, long Events);
