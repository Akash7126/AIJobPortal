using FluentValidation;

namespace JobPlatform.Reporting.Application.Validators.Common;

/// <summary>DateRangeValidator (handover 7): from at most to, range at most 24 months, granularity one of day, week, month.</summary>
public static class DateRangeRules
{
    public static void AddTo<T>(AbstractValidator<T> validator, Func<T, DateOnly?> from, Func<T, DateOnly?> to, Func<T, string?>? granularity = null)
    {
        validator.RuleFor(x => x).Must(x => from(x) is null || to(x) is null || from(x) <= to(x)).OverridePropertyName("from").WithErrorCode("VAL.DateRange.Inverted");
        validator.RuleFor(x => x).Must(x => from(x) is null || to(x) is null || to(x)!.Value.ToDateTime(TimeOnly.MinValue) <= from(x)!.Value.ToDateTime(TimeOnly.MinValue).AddMonths(DateRanges.MaxMonths))
            .OverridePropertyName("to").WithErrorCode("VAL.DateRange.TooLong");
        if (granularity is not null)
        {
            validator.RuleFor(x => granularity(x)).Must(g => g is null || DateRanges.Granularities.Contains(g, StringComparer.OrdinalIgnoreCase))
                .OverridePropertyName("granularity").WithErrorCode("VAL.Granularity.Invalid");
        }
    }
}
