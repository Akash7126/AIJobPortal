using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Domain;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

internal sealed class GetCandidateRecommendationsHandler(IKnownPostingRepository postings, IMatchReadStore store, IMatchingConfigurationProvider configuration,
    ICurrentUser user) : IQueryHandler<GetCandidateRecommendationsQuery, PagedResult<MatchedCandidateDto>>
{
    public async Task<Result<PagedResult<MatchedCandidateDto>>> Handle(GetCandidateRecommendationsQuery request, CancellationToken ct)
    {
        var owned = await PostingOwnership.RequireOwnedAsync(postings, user, request.JobPostingId, AiErrorCodes.CandidateRecommendationForbidden, ct);
        if (owned.IsFailure)
        {
            return owned.Error!;
        }

        var page = new PageRequest(request.Page, request.PageSize);
        if (!owned.Value.IsActive)
        {
            return PageMapping.Of(Array.Empty<MatchedCandidateDto>(), page, 0); // suitable seekers exist only for active postings
        }

        var threshold = (await configuration.GetAsync(ct)).MatchThresholdPercent;
        return await store.ListCandidatesAsync(request.JobPostingId, threshold, page, ct);
    }
}
