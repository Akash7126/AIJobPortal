using JobPlatform.JobSeekerProfile.Application.DTOs.Common;

namespace JobPlatform.JobSeekerProfile.Application.DTOs.ShareLink;

public sealed record SharedProfileView(
    string FullName, IReadOnlyList<SkillView> Skills, IReadOnlyList<EducationView> Education, IReadOnlyList<ExperienceView> Experience, string? Statement,
    string? Bio);
