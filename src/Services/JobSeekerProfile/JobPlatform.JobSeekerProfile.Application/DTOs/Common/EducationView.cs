namespace JobPlatform.JobSeekerProfile.Application.DTOs.Common;

public sealed record EducationView(Guid Id, string Degree, string Institution, DateTime? From, DateTime? To, string Source);
