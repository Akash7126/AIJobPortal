using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.AiMatching.Domain;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;

namespace JobPlatform.AiMatching.Application.Queries.Matching;

/// <summary>US-3.3.2-02: suitable seekers for an owned, active posting (owner-only, E-JRE-FORBIDDEN). BC-11 owns the employer-facing ranking (Q-05).</summary>
public sealed record GetCandidateRecommendationsQuery(Guid JobPostingId, int Page = 1, int PageSize = 20)
    : EmployerRequest(AiErrorCodes.CandidateRecommendationForbidden), IQuery<PagedResult<MatchedCandidateDto>>;
