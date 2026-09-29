using JobPlatform.JobSeekerProfile.Application.DTOs.Common;

namespace JobPlatform.JobSeekerProfile.Application.DTOs.Preferences;

public sealed record JobPreferenceView(
    Guid ProfileId, IReadOnlyList<string> JobTypes, IReadOnlyList<string> Industries, IReadOnlyList<string> Locations, SalaryRangeView? SalaryExpectation,
    IReadOnlyList<string> WorkArrangements, DateTime UpdatedAtUtc);
