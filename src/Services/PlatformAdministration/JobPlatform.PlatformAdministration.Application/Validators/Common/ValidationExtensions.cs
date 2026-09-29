using FluentValidation;
using JobPlatform.PlatformAdministration.Application.Offerings;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.PlatformAdministration.Application.Validators.Common;

/// <summary>Rule fragments shared by the PlatformAdministration validators.</summary>
public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.OutOfRange");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.OutOfRange");

    /// <summary>Moderation reason (suspend/remove a job offering): required, trimmed length within <see cref="ModerationRules"/>.</summary>
    public static IRuleBuilderOptions<T, string?> ValidModerationReason<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithErrorCode("VAL.Reason.Required")
            .Must(r => r is null || r.Trim().Length is >= ModerationRules.MinReasonLength and <= ModerationRules.MaxReasonLength).WithErrorCode("VAL.Reason.OutOfRange");
}
