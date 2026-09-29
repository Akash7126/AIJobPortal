using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Profile;

public sealed record UpdateExperienceCommand(IReadOnlyList<ExperienceInput> Entries, decimal? YearsOfExperience, string? IfMatch) : JobSeekerCommand<Unit>;
