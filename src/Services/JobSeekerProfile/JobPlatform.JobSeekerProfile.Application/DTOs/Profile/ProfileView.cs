using JobPlatform.JobSeekerProfile.Application.DTOs.Common;

namespace JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

public sealed record ProfileView(
    Guid ProfileId, Guid OwnerAccountId, string Status, string FullName, string Email, string MobileNumber, string Gender,
    IReadOnlyList<EducationView> Education, IReadOnlyList<ExperienceView> Experience, IReadOnlyList<SkillView> Skills,
    IReadOnlyList<TrainingView> Training, IReadOnlyList<CertificateView> Certificates, SalaryRangeView? SalaryExpectation, AddressView? Address,
    decimal? YearsOfExperience, IReadOnlyList<SocialLinkView> SocialLinks, string? Statement, string? Bio, int CompletionPercent, byte[] RowVersion);
