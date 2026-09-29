namespace JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

public sealed record ProfileCompletionView(int Percent, IReadOnlyList<string> MissingSections);
