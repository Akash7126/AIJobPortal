using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Profile;

public sealed record UpdateLevel1Command(string FullName, string Email, string MobileNumber, string Gender, string? IfMatch) : JobSeekerCommand<Unit>;
