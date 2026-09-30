using JobPlatform.AiMatching.Application.DTOs.Recommendations;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;

namespace JobPlatform.AiMatching.Application.Queries.Recommendations;

/// <summary>US-3.3.2-01: the caller's latest personalised job recommendations (computed on first use, then refreshed weekly by the scheduler).</summary>
public sealed record GetJobRecommendationsQuery : SeekerRequest, IQuery<JobRecommendationDto>;
