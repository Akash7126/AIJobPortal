using FluentValidation;

namespace JobPlatform.HelpContent.Application.Validators.Common;

/// <summary>Shared title/body validation for both News and Help authoring commands (handover section 7).</summary>
internal sealed class TitleBodyRules<T> : AbstractValidator<T>
{
    public TitleBodyRules(System.Linq.Expressions.Expression<Func<T, string?>> titleAr, System.Linq.Expressions.Expression<Func<T, string?>> titleEn,
        System.Linq.Expressions.Expression<Func<T, string?>> bodyAr, System.Linq.Expressions.Expression<Func<T, string?>> bodyEn)
    {
        var getTitleAr = titleAr.Compile();
        var getTitleEn = titleEn.Compile();
        var getBodyAr = bodyAr.Compile();
        var getBodyEn = bodyEn.Compile();

        RuleFor(titleAr).MaximumLength(200).WithErrorCode("VAL.TitleAr.TooLong");
        RuleFor(titleEn).MaximumLength(200).WithErrorCode("VAL.TitleEn.TooLong");
        RuleFor(bodyAr).MaximumLength(50_000).WithErrorCode("VAL.BodyAr.TooLong");
        RuleFor(bodyEn).MaximumLength(50_000).WithErrorCode("VAL.BodyEn.TooLong");
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(getTitleAr(c)) || !string.IsNullOrWhiteSpace(getTitleEn(c)))
            .WithErrorCode("VAL.Title.Required").OverridePropertyName("Title");
        RuleFor(c => c).Must(c => !string.IsNullOrWhiteSpace(getBodyAr(c)) || !string.IsNullOrWhiteSpace(getBodyEn(c)))
            .WithErrorCode("VAL.Body.Required").OverridePropertyName("Body");
    }
}
