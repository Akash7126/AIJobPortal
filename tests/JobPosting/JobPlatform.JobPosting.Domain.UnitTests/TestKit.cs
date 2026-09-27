using JobPlatform.JobPosting.Domain;
using JobPlatform.SharedKernel.Common.ValueObjects;

namespace JobPlatform.JobPosting.Domain.UnitTests;

internal static class TestKit
{
    public static readonly DateTime At = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    public static JobPostingFields Fields(DateTime? deadline = null, IReadOnlyList<string>? skills = null, string category = "software-development") => new(
        new LocalizedText("مهندس برمجيات", "Software Engineer"),
        new LocalizedText("وصف الوظيفة يجب أن يكون طويلا بما فيه الكفاية ليجتاز التحقق من الصحة.", "A sufficiently long job summary that satisfies validation minimum length."),
        skills ?? new[] { "csharp", "sql" }, category, ContractType.FullTime, EducationLevel.Bachelor, Array.Empty<string>(), WorkFormat.Hybrid,
        Domain.JobLocation.Create("Ramallah", "Ramallah"), SalaryRange.Create(1000, 2000, "ILS"), 2, 5, new[] { "ar", "en" },
        ApplicationDeadline.Create(deadline ?? At.AddDays(30), true), "https://example.com/apply", null);

    public static Actor Employer(Guid id) => new(id, false, false);

    public static Actor Administrator() => new(Guid.NewGuid(), true, false);

    public static Actor System() => Actor.System(Guid.Empty);

    public static JobPlatform.JobPosting.Domain.JobPosting Draft(Guid? employerId = null, DateTime? deadline = null)
    {
        var id = employerId ?? Guid.NewGuid();
        return JobPlatform.JobPosting.Domain.JobPosting.CreateDraft(id, Fields(deadline), Employer(id), 1, ContentHasher.Hash(Fields(deadline)), At);
    }

    public static JobPlatform.JobPosting.Domain.JobPosting Active(Guid? employerId = null, DateTime? deadline = null)
    {
        var id = employerId ?? Guid.NewGuid();
        var posting = JobPlatform.JobPosting.Domain.JobPosting.CreateDraft(id, Fields(deadline), Employer(id), 1, ContentHasher.Hash(Fields(deadline)), At);
        posting.ClearDomainEvents();
        posting.Publish(Employer(id), At);
        return posting;
    }
}
