using FluentValidation;
using JobPlatform.Reporting.Application.Commands.Performance;
using JobPlatform.Reporting.Application.Validators.Common;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Validators.Performance;

/// <summary>ConfigurePerformanceAlertRuleValidator (handover 7): metric known, threshold numeric, window at least one minute.</summary>
public sealed class ConfigurePerformanceAlertRuleValidator : AbstractValidator<ConfigurePerformanceAlertRuleCommand>
{
    public ConfigurePerformanceAlertRuleValidator()
    {
        RuleFor(x => x.Metric).KnownMetric();
        RuleFor(x => x.Comparator).Must(c => Enum.TryParse<AlertComparator>(c, true, out _)).WithErrorCode("VAL.Comparator.Invalid");
        RuleFor(x => x.Severity).Must(s => Enum.TryParse<AlertSeverity>(s, true, out _)).WithErrorCode("VAL.Severity.Invalid");
        RuleFor(x => x.WindowMinutes).GreaterThanOrEqualTo(1).WithErrorCode("VAL.Window.TooShort");
        RuleFor(x => x.Threshold).InclusiveBetween(-1_000_000_000m, 1_000_000_000m).WithErrorCode("VAL.Threshold.OutOfRange");
    }
}
