using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Profile;

public sealed record UpdateTrainingCommand(IReadOnlyList<TrainingInput> Entries, string? IfMatch) : JobSeekerCommand<Unit>;
