namespace JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

public sealed record TrainingView(Guid Id, string Name, string? Provider, DateTime? CompletedOn);
