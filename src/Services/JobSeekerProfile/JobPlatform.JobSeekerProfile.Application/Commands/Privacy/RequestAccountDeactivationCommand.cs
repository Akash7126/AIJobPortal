using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Privacy;

public sealed record RequestAccountDeactivationCommand(string? Reason) : JobSeekerCommand<Unit>;
