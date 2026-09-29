namespace JobPlatform.JobSeekerProfile.Application.DTOs.Common;

public sealed record SkillView(Guid Id, string Name, string Kind, string Class, string Source);
