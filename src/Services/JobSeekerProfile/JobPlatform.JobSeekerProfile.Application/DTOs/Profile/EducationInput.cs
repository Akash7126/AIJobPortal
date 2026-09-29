namespace JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

public sealed record EducationInput(string Degree, string Institution, DateTime? From, DateTime? To);
