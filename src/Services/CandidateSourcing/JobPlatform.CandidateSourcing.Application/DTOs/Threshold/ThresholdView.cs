namespace JobPlatform.CandidateSourcing.Application.DTOs.Threshold;

public sealed record ThresholdView(Guid JobPostingId, int Percent, int Version);
