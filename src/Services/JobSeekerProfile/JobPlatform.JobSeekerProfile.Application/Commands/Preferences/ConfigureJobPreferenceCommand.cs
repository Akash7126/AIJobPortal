using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.JobSeekerProfile.Application.Commands.Preferences;

public sealed record ConfigureJobPreferenceCommand(
    IReadOnlyList<string> JobTypes, IReadOnlyList<string> Industries, IReadOnlyList<string> Locations, decimal? SalaryMin, decimal? SalaryMax,
    string? SalaryCurrency, IReadOnlyList<string> WorkArrangements) : JobSeekerCommand<Unit>;
