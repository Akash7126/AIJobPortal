namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record HistoryPointDto(string Period, decimal? Average, decimal? Max, long Samples);
