using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Performance;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.Performance;

public sealed class GetUsagePatternsValidator : AbstractValidator<GetUsagePatternsQuery>
{
    public GetUsagePatternsValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}
