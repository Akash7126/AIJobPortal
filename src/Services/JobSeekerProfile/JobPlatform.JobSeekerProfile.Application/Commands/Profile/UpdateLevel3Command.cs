using JobPlatform.JobSeekerProfile.Application.DTOs.Profile;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Profile;

public sealed record UpdateLevel3Command(IReadOnlyList<SocialLinkInput> SocialLinks, string? Statement, string? Bio, string? IfMatch) : JobSeekerCommand<Unit>;
