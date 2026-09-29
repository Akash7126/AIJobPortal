using FluentValidation;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.Notification.Application.Validators.Common;

/// <summary>Rule fragments shared by the Notification validators.</summary>
public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");
}
