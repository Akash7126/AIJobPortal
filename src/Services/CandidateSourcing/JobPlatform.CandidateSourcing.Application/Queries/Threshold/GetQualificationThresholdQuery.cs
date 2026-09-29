using JobPlatform.CandidateSourcing.Application.DTOs.Threshold;

namespace JobPlatform.CandidateSourcing.Application.Queries.Threshold;

public sealed record GetQualificationThresholdQuery(Guid JobPostingId) : EmployerQuery<ThresholdView>;
