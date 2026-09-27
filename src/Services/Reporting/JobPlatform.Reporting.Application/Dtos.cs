using System.Text.Json;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application;

// Read models and API shapes. Every figure that can be too thin carries an explicit insufficientData flag instead of a misleading number.

/// <summary>A single figure, or an explicit "no figure" with the reason (insufficient data, no data source).</summary>
public sealed record FigureDto(bool InsufficientData, decimal? Value, long SampleSize, string? Reason = null);

public sealed record ActivityCountDto(string Type, long Count);

public sealed record UserActivityDto(DateOnly From, DateOnly To, long Total, IReadOnlyList<ActivityCountDto> Items);

public sealed record RetentionPolicyDto(int RetentionMonths, int LegalMinimumMonths, DateTime UpdatedAtUtc);

public sealed record LoginDashboardDto(bool SourceAvailable, int CurrentCount, DateTime? LastSessionAtUtc, IReadOnlyList<ActiveSession> Sessions);

public sealed record PostingCountsDto(long Total, long Active, long Closed, long External);

public sealed record RegistrationCountsDto(long JobSeekers, long Employers, long ProfilesCreated, long AccountsApproved);

public sealed record EmploymentStatisticsDto(DateOnly From, DateOnly To, bool InsufficientData, long SampleSize, PostingCountsDto? Postings, RegistrationCountsDto? Registrations);

public sealed record TrendPointDto(string Period, long Count);

public sealed record EmploymentMetricsDto(DateOnly From, DateOnly To, string Granularity, bool InsufficientData, IReadOnlyList<TrendPointDto> PostingTrend, FigureDto ApplicationRate,
    FigureDto HiringRate, FigureDto TimeToClose);

public sealed record IndustryRowDto(string Industry, long? Demand, long? ActivePostings, bool InsufficientData);

public sealed record IndustryAnalyticsDto(DateOnly From, DateOnly To, bool InsufficientData, bool SupplyDataAvailable, IReadOnlyList<IndustryRowDto> Rows);

public sealed record SkillTrendItemDto(string Skill, long Demand, long PreviousDemand, decimal? Growth, long Supply, long Gap, bool Emerging);

public sealed record SkillTrendsDto(DateOnly From, DateOnly To, string Granularity, bool InsufficientHistory, IReadOnlyList<SkillTrendItemDto> Items);

public sealed record GeographyRowDto(string Location, long? Count, bool Suppressed);

public sealed record GeographyDto(DateOnly From, DateOnly To, IReadOnlyList<GeographyRowDto> Jobs, IReadOnlyList<GeographyRowDto> Candidates);

public sealed record SalaryRowDto(string Group, long? Count, decimal? Min, decimal? Average, decimal? Max, bool Suppressed);

public sealed record SalaryAnalyticsDto(DateOnly From, DateOnly To, string By, bool InsufficientData, IReadOnlyList<SalaryRowDto> Rows);

public sealed record OutcomeItemDto(Guid JobPostingId, int Shortlisted, int FollowUps, decimal? AverageFit);

public sealed record EmploymentOutcomesDto(DateOnly From, DateOnly To, IReadOnlyList<OutcomeItemDto> Items, int ExcludedWithoutFollowUp);

public sealed record LaborMarketReportDto(Guid Id, string Period, DateTime GeneratedAtUtc, JsonElement Content, bool Existing);

public sealed record MetricSummaryDto(string Metric, decimal? Latest, decimal? Average, decimal? Max, long Samples);

public sealed record MatchingMetricsDto(long ScoresComputed, decimal? AverageScore, long RecommendationSets, FigureDto Accuracy, FigureDto Precision, FigureDto Recall,
    FigureDto Satisfaction);

public sealed record SystemPerformanceDto(DateOnly From, DateOnly To, bool NoTechnicalData, MatchingMetricsDto Matching, IReadOnlyList<MetricSummaryDto> Technical);

public sealed record HourCountDto(int Hour, long Count);

public sealed record UsagePatternsDto(DateOnly From, DateOnly To, IReadOnlyList<HourCountDto> PeakHours, IReadOnlyList<ActivityCountDto> PopularFeatures, long ActiveActors,
    decimal AverageDailyActiveActors, long Events);

public sealed record AlertRuleDto(Guid Id, string Metric, string Comparator, decimal Threshold, int WindowMinutes, string Severity, bool Enabled);

public sealed record AlertDto(Guid Id, Guid RuleId, string Metric, string Severity, decimal Value, decimal Threshold, DateTime RaisedAtUtc);

public sealed record PerformanceDashboardDto(SystemPerformanceDto Performance, UsagePatternsDto Usage, IReadOnlyList<AlertDto> RecentAlerts);

public sealed record HistoryPointDto(string Period, decimal? Average, decimal? Max, long Samples);

public sealed record MetricHistoryDto(string Metric, DateOnly From, DateOnly To, string Granularity, IReadOnlyList<HistoryPointDto> Points);

// ---------------------------------------------------------------------- report framework

public sealed record TemplateParameterDto(string Name, string Type, string? Min, string? Max, string? Default, IReadOnlyList<string>? Options);

public sealed record TemplateDto(Guid Id, string Name, string DataSource, IReadOnlyList<TemplateParameterDto> Parameters, int Revision, DateTime UpdatedAtUtc);

public sealed record ScheduleDto(Guid Id, string Name, Guid? TemplateId, Guid? SavedReportId, string Interval, string? Cron, IReadOnlyList<string> Recipients, string Format,
    DateTime NextRunAtUtc, DateTime? LastRunAtUtc, bool Active);

public sealed record ExportDto(Guid Id, string RefKind, Guid? RefId, string Format, string Status, string? ResultRef, string? FailureReason, DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc, bool Reused);

public sealed record SavedReportDto(Guid Id, string Name, ReportDefinitionDto Definition, DateTime CreatedAtUtc, bool Archived);

public sealed record ReportFilterDto(string Field, string Operator, string Value);

public sealed record ReportDefinitionDto(string DataSource, IReadOnlyList<string> Fields, IReadOnlyList<ReportFilterDto> Filters, IReadOnlyList<string> GroupBy);

public sealed record BuilderFieldDto(string Name, string Kind, string Aggregation);

public sealed record AccessRuleDto(string Role, IReadOnlyList<string> Categories, DateTime UpdatedAtUtc);

/// <summary>Result of running a report. Fallback = Power BI was requested but the upstream timed out, so the built-in view is returned with the warning code.</summary>
public sealed record ReportResultDto(string Target, bool Fallback, string? WarningCode, string? PowerBiUrl, string Title, ReportTable Table);

public sealed record ChartSeriesDto(string Name, IReadOnlyList<decimal?> Values);

/// <summary>Rendered view of a report result: tabular, chart spec or visualisation spec (US-3.5.4-03).</summary>
public sealed record ReportViewDto(string Format, string Title, ReportTable? Table, IReadOnlyList<string>? Labels, IReadOnlyList<ChartSeriesDto>? Series, string? ChartType,
    IReadOnlyList<KeyValuePair<string, decimal?>>? Highlights);
