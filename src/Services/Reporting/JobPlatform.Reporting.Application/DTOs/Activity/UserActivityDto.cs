using JobPlatform.Reporting.Application.DTOs.Common;

namespace JobPlatform.Reporting.Application.DTOs.Activity;

public sealed record UserActivityDto(DateOnly From, DateOnly To, long Total, IReadOnlyList<ActivityCountDto> Items);
