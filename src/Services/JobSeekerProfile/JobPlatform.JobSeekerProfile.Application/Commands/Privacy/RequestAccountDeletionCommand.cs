using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Privacy;

public sealed record RequestAccountDeletionCommand(bool Confirm) : JobSeekerCommand<Unit>;
