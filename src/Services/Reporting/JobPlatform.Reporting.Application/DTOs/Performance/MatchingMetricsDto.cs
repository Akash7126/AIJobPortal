using JobPlatform.Reporting.Application.DTOs.Common;

namespace JobPlatform.Reporting.Application.DTOs.Performance;

public sealed record MatchingMetricsDto(long ScoresComputed, decimal? AverageScore, long RecommendationSets, FigureDto Accuracy, FigureDto Precision, FigureDto Recall,
    FigureDto Satisfaction);
