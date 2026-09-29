using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.Employment;

public sealed class GetIndustryAnalyticsValidator : AbstractValidator<GetIndustryAnalyticsQuery>
{
    public GetIndustryAnalyticsValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To);
}
