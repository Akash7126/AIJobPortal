using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Employment;
using JobPlatform.Reporting.Application.Validators.Common;

namespace JobPlatform.Reporting.Application.Validators.Employment;

public sealed class GetSalaryAnalyticsValidator : AbstractValidator<GetSalaryAnalyticsQuery>
{
    public static readonly string[] Groupings = { "industry", "position", "location" };

    public GetSalaryAnalyticsValidator()
    {
        DateRangeRules.AddTo(this, x => x.From, x => x.To);
        RuleFor(x => x.By).Must(b => b is null || Groupings.Contains(b, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.By.Unknown");
    }
}
