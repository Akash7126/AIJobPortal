using FluentValidation;

namespace JobPlatform.JobPosting.Application.Validators.Postings;

/// <summary>Field rules shared by the create and update job posting validators.</summary>
internal static class SharedFieldRules
{
    public static void Apply<T>(AbstractValidator<T> v, Func<T, string> ar, Func<T, string> en, Func<T, string> summaryAr, Func<T, string> summaryEn,
        Func<T, IReadOnlyList<string>> skills, Func<T, string?> jobLink, Func<T, DateTime> deadline, Func<T, IReadOnlyList<string>?> requiredLanguages,
        Func<T, decimal?> salaryMin, Func<T, decimal?> salaryMax, Func<T, IReadOnlyDictionary<string, string>?> otherFields)
    {
        v.RuleFor(x => ar(x)).NotEmpty().WithErrorCode("VAL.TitleAr.Required")
            .Length(PostingFieldRules.TitleMin, PostingFieldRules.TitleMax).WithErrorCode("VAL.TitleAr.OutOfRange");
        v.RuleFor(x => en(x)).NotEmpty().WithErrorCode("VAL.TitleEn.Required")
            .Length(PostingFieldRules.TitleMin, PostingFieldRules.TitleMax).WithErrorCode("VAL.TitleEn.OutOfRange");
        v.RuleFor(x => summaryAr(x)).NotEmpty().WithErrorCode("VAL.SummaryAr.Required")
            .Length(PostingFieldRules.SummaryMin, PostingFieldRules.SummaryMax).WithErrorCode("VAL.SummaryAr.OutOfRange");
        v.RuleFor(x => summaryEn(x)).NotEmpty().WithErrorCode("VAL.SummaryEn.Required")
            .Length(PostingFieldRules.SummaryMin, PostingFieldRules.SummaryMax).WithErrorCode("VAL.SummaryEn.OutOfRange");
        v.RuleFor(x => skills(x)).Must(s => s is { Count: >= PostingFieldRules.MinSkills and <= PostingFieldRules.MaxSkills })
            .WithErrorCode("VAL.Skills.OutOfRange");
        v.RuleFor(x => jobLink(x)).Must(link => link is null || Uri.TryCreate(link, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https"))
            .WithErrorCode("VAL.JobLink.Invalid");
        v.RuleFor(x => deadline(x)).Must((x, d) => salaryMin(x) is null || salaryMax(x) is null || salaryMin(x) <= salaryMax(x))
            .WithErrorCode("VAL.Salary.MinGreaterThanMax");
        v.RuleFor(x => requiredLanguages(x)).Must(langs => langs is null || langs.All(l => PostingFieldRules.RequiredLanguageCodes.Contains(l.ToLowerInvariant())))
            .WithErrorCode("VAL.RequiredLanguages.Invalid");
        v.RuleFor(x => otherFields(x)).Must(f => f is null || f.Count <= 20).WithErrorCode("VAL.OtherFields.TooMany");
    }
}
