using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Privacy;

public sealed record SetProfileVisibilityCommand(bool Public, bool PublicSharingActive) : JobSeekerCommand<Unit>;
