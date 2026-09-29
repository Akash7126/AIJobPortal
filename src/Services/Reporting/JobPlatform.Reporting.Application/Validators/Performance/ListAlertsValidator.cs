using FluentValidation;
using JobPlatform.Reporting.Application.Queries.Performance;

namespace JobPlatform.Reporting.Application.Validators.Performance;

public sealed class ListAlertsValidator : AbstractValidator<ListAlertsQuery>
{
    public ListAlertsValidator() => RuleFor(x => x.Take).InclusiveBetween(1, 200).WithErrorCode("VAL.Take.OutOfRange");
}
