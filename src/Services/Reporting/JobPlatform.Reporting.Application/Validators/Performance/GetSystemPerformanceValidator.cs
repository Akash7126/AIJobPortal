using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.Performance;

public sealed class GetSystemPerformanceValidator : AbstractValidator<GetSystemPerformanceQuery>
{
    public GetSystemPerformanceValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}
