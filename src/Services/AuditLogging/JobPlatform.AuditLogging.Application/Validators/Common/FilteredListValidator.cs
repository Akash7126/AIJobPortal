using FluentValidation;
using JobPlatform.AuditLogging.Application.Interfaces;

namespace JobPlatform.AuditLogging.Application.Validators.Common;

public abstract class FilteredListValidator<TQuery> : AbstractValidator<TQuery> where TQuery : IFilteredListQuery
{
    private static readonly string[] Outcomes = { "success", "failure", "duplicate", "denied" };

    protected FilteredListValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        When(x => x.From.HasValue && x.To.HasValue, () =>
            RuleFor(x => x.To!.Value).GreaterThanOrEqualTo(x => x.From!.Value).OverridePropertyName("To").WithErrorCode("VAL.To.BeforeFrom"));
        When(x => !string.IsNullOrEmpty(x.Outcome), () =>
            RuleFor(x => x.Outcome!).Must(o => Outcomes.Contains(o.ToLowerInvariant())).OverridePropertyName("Outcome").WithErrorCode("VAL.Outcome.Invalid"));
    }
}
