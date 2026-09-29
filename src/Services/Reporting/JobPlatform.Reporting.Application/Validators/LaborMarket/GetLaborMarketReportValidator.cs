using FluentValidation;
using JobPlatform.Reporting.Application.Queries.LaborMarket;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.LaborMarket;

public sealed class GetLaborMarketReportValidator : AbstractValidator<GetLaborMarketReportQuery>
{
    public GetLaborMarketReportValidator() =>
        RuleFor(x => x.Period).ValidPeriodWhenPresent();
}
