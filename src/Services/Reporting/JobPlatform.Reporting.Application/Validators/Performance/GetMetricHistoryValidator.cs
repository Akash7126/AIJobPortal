using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.Performance;

public sealed class GetMetricHistoryValidator : AbstractValidator<GetMetricHistoryQuery>
{
    public GetMetricHistoryValidator()
    {
        RuleFor(x => x.Metric).KnownMetric();
        DateRangeRules.AddTo(this, x => x.From, x => x.To, x => x.Granularity);
    }
}
