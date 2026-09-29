namespace JobPlatform.JobPosting.Application.Validators.Postings;

public static class PostingFieldRules
{
    public const int TitleMin = 3, TitleMax = 200;
    public const int SummaryMin = 20, SummaryMax = 5000;
    public const int MinSkills = 1, MaxSkills = 30;
    public static readonly string[] RequiredLanguageCodes = { "ar", "en" };
}
