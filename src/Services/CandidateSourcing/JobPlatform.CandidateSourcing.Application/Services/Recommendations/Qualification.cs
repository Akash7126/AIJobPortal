using JobPlatform.SharedKernel.ApiContracts.AiMatching;

namespace JobPlatform.CandidateSourcing.Application.Services.Recommendations;

/// <summary>Result of <see cref="CandidateQualificationService"/>: whether the caller may see candidates, and the qualified candidates with their score breakdown.</summary>
internal readonly record struct Qualification(bool Forbidden, IReadOnlyList<(Guid ProfileId, decimal Score, MatchCriterionDto[] Breakdown)> Candidates);
