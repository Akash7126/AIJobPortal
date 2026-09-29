using FluentValidation;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Validators.Common;

/// <summary>Rule fragments shared by the Reporting validators: names and the string-typed enumerations of the reporting API.</summary>
public static class ValidationExtensions
{
    public const int MaxNameLength = 150;

    public static IRuleBuilderOptions<T, string?> ValidReportName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithErrorCode("VAL.Name.Required").MaximumLength(MaxNameLength).WithErrorCode("VAL.Name.TooLong");

    public static IRuleBuilderOptions<T, string?> KnownReportFormat<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(f => Enum.TryParse<ReportFormat>(f, true, out _)).WithErrorCode("VAL.Format.Unknown");

    public static IRuleBuilderOptions<T, string?> KnownDataSource<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(s => Enum.TryParse<ReportDataSource>(s, true, out _)).WithErrorCode("VAL.DataSource.Unknown");

    /// <summary>Optional report target: absent, or a known <see cref="ReportTarget"/>.</summary>
    public static IRuleBuilderOptions<T, string?> KnownTargetWhenPresent<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(t => t is null || Enum.TryParse<ReportTarget>(t, true, out _)).WithErrorCode("VAL.Target.Unknown");

    public static IRuleBuilderOptions<T, string?> KnownMetric<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(PerformanceMetrics.IsKnown).WithErrorCode("VAL.Metric.Unknown");

    /// <summary>Optional labour-market period: absent, or parseable by <see cref="LaborMarketReport.TryParsePeriod"/>.</summary>
    public static IRuleBuilderOptions<T, string?> ValidPeriodWhenPresent<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(p => p is null || LaborMarketReport.TryParsePeriod(p, out _, out _)).WithErrorCode("VAL.Period.Invalid");
}
