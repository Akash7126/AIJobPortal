using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Profile;

public sealed record CreateProfileCommand(string FullName, string Email, string MobileNumber, string Gender) : JobSeekerCommand<ProfileView>;
