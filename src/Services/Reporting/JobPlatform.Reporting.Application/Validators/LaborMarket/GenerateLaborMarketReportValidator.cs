using FluentValidation;
using JobPlatform.Reporting.Application.Commands.LaborMarket;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.LaborMarket;

public sealed class GenerateLaborMarketReportValidator : AbstractValidator<GenerateLaborMarketReportCommand>
{
    public GenerateLaborMarketReportValidator() =>
        RuleFor(x => x.Period).ValidPeriodWhenPresent();
}
