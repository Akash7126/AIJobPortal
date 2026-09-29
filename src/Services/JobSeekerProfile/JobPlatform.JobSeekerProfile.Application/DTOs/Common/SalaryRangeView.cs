namespace JobPlatform.JobSeekerProfile.Application.DTOs.Common;

public sealed record SalaryRangeView(decimal? Min, decimal? Max, string? Currency);
