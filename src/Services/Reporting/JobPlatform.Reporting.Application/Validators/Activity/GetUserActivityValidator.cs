using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Activity;
using JobPlatform.Reporting.Application.Validators.Common;
using JobPlatform.Reporting.Domain;

namespace JobPlatform.Reporting.Application.Validators.Activity;

public sealed class GetUserActivityValidator : AbstractValidator<GetUserActivityQuery>
{
    public GetUserActivityValidator()
    {
        DateRangeRules.AddTo(this, x => x.From, x => x.To);
        RuleFor(x => x.Type).Must(t => t is null || ActivityTypes.All.Contains(t, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.Type.Unknown");
    }
}
