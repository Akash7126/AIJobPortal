using FluentValidation;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.AccountIdentity.Application.Validators.Common;

/// <summary>Rule fragments shared by the AccountIdentity validators, so each input shape is checked (and reported) the same way everywhere.</summary>
public static class ValidationExtensions
{
    public const int MaxPasswordLength = 128;
    public const int MaxReasonLength = 500;
    public const int MaxPermissionLength = 100;

    public static IRuleBuilderOptions<T, string> ValidMobile<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => MobileNumber.TryCreate(v, out _)).WithErrorCode("VAL.MobileNumber.Invalid");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => Email.TryCreate(v, out _)).WithErrorCode("VAL.Email.Invalid");

    /// <summary>Required mobile number; the format is only checked when a value is present.</summary>
    public static IRuleBuilderOptions<T, string> RequiredMobile<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop).NotEmpty().WithErrorCode("VAL.MobileNumber.Required").ValidMobile();

    /// <summary>Required e-mail address; the format is only checked when a value is present.</summary>
    public static IRuleBuilderOptions<T, string> RequiredEmail<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop).NotEmpty().WithErrorCode("VAL.Email.Required").ValidEmail();

    /// <summary>Presence and a length cap only: password strength is a domain policy (PasswordPolicy).</summary>
    public static IRuleBuilderOptions<T, string?> RequiredPassword<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithErrorCode("VAL.Password.Required").MaximumLength(MaxPasswordLength).WithErrorCode("VAL.Password.MaxLength");

    public static IRuleBuilderOptions<T, string?> RequiredReason<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithErrorCode("VAL.Reason.Required").MaximumLength(MaxReasonLength).WithErrorCode("VAL.Reason.TooLong");

    public static IRuleBuilderOptions<T, string?> RequiredPermission<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithErrorCode("VAL.Permission.Required").MaximumLength(MaxPermissionLength).WithErrorCode("VAL.Permission.TooLong");

    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithErrorCode("VAL.Page.Invalid");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, PageRequest.MaxPageSize).WithErrorCode("VAL.PageSize.Invalid");
}
