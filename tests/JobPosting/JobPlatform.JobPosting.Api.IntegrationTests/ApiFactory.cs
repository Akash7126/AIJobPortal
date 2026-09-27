using JobPlatform.TestSupport;

namespace JobPlatform.JobPosting.Api.IntegrationTests;

/// <summary>Hosts the real Job Posting API in-process (SQLite, in-memory cache and bus, fake clock, inline JWKS, fake taxonomy/standing/ranking).</summary>
public sealed class ApiFactory : ApiTestFactory<Program>
{
    protected override string ConnectionStringName => "JobPosting";

    protected override Dictionary<string, string?> Settings()
    {
        var settings = base.Settings();
        settings["Taxonomy:Provider"] = "Fake";
        settings["EmployerStanding:Provider"] = "Fake";
        settings["MatchRanking:Provider"] = "Fake";
        settings["Jobs:Enabled"] = "false";
        return settings;
    }

    public static object PostingBody(string titleEn = "Backend Engineer", string[]? skills = null, DateTime? deadlineUtc = null) => new
    {
        titleAr = "مهندس برمجيات",
        titleEn,
        summaryAr = "وصف طويل بما فيه الكفاية لتجاوز الحد الأدنى للتحقق من الصحة في هذا الاختبار.",
        summaryEn = "A sufficiently long job summary that satisfies the minimum length validation rule for this integration test.",
        skills = skills ?? new[] { "csharp", "sql" },
        categoryCode = "software-development",
        contractType = "FullTime",
        educationLevel = "Bachelor",
        requiredTraining = Array.Empty<string>(),
        workFormat = "Hybrid",
        governorate = "Ramallah",
        city = "Ramallah",
        salaryMin = 1000,
        salaryMax = 2000,
        salaryCurrency = "ILS",
        minExperienceYears = 1,
        maxExperienceYears = 5,
        requiredLanguages = new[] { "ar", "en" },
        deadlineUtc = deadlineUtc ?? DateTime.UtcNow.AddDays(30),
        autoClose = true,
        jobLink = "https://example.com/apply",
        otherFields = (Dictionary<string, string>?)null
    };
}
