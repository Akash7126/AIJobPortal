using FluentValidation;
using JobPlatform.JobSeekerProfile.Application.Profile;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.JobSeekerProfile.Application.Validators.Common;

/// <summary>Level-1 personal-detail rules shared by profile creation and the Level-1 update, so both report the same errors.</summary>
public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(2, 100).WithErrorCode("VAL.FullName.Required");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(e => Email.TryCreate(e, out _)).WithErrorCode("VAL.Email.Invalid");

    public static IRuleBuilderOptions<T, string> ValidMobile<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(m => MobileNumber.TryCreate(m, out _)).WithErrorCode("VAL.MobileNumber.Invalid");

    public static IRuleBuilderOptions<T, string> ValidGender<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Must(g => GenderValues.Allowed.Contains(g, StringComparer.OrdinalIgnoreCase)).WithErrorCode("VAL.Gender.Invalid");
}
