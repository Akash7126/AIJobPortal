using FluentValidation;

namespace JobPlatform.AiMatching.Application.Validators.Common;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule, int max = 50) =>
        rule.InclusiveBetween(1, max).WithErrorCode("VAL.PageSize.OutOfRange");

    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");
}
