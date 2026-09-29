namespace JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

public sealed record ExperienceInput(string Company, string Role, DateTime? From, DateTime? To);
