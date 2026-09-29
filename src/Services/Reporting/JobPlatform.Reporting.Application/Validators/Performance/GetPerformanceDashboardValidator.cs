using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.Performance;

public sealed class GetPerformanceDashboardValidator : AbstractValidator<GetPerformanceDashboardQuery>
{
    public GetPerformanceDashboardValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}
