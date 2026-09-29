namespace JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

public sealed record TrainingInput(string Name, string? Provider, DateTime? CompletedOn);
