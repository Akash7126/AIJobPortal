namespace JobPlatform.Reporting.Application.DTOs.Employment;

public sealed record OutcomeItemDto(Guid JobPostingId, int Shortlisted, int FollowUps, decimal? AverageFit);
