namespace JobPlatform.JobSeekerProfile.Application.DTOs.Common;

public sealed record ExperienceView(Guid Id, string Company, string Role, DateTime? From, DateTime? To, string Source);
