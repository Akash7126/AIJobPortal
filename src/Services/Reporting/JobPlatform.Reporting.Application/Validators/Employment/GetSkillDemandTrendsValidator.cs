using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.Employment;

public sealed class GetSkillDemandTrendsValidator : AbstractValidator<GetSkillDemandTrendsQuery>
{
    public GetSkillDemandTrendsValidator() => DateRangeRules.AddTo(this, x => x.From, x => x.To, x => x.Granularity);
}
