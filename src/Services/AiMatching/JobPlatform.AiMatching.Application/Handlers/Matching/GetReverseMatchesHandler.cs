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

internal sealed class GetReverseMatchesHandler(IKnownPostingRepository postings, IMatchReadStore store, IMatchingConfigurationProvider configuration, ICurrentUser user)
    : IQueryHandler<GetReverseMatchesQuery, PagedResult<MatchedCandidateDto>>
{
    public async Task<Result<PagedResult<MatchedCandidateDto>>> Handle(GetReverseMatchesQuery request, CancellationToken ct)
    {
        var owned = await PostingOwnership.RequireOwnedAsync(postings, user, request.JobPostingId, AiErrorCodes.Forbidden, ct);
        if (owned.IsFailure)
        {
            return owned.Error!;
        }

        var threshold = (await configuration.GetAsync(ct)).MatchThresholdPercent;
        return await store.ListCandidatesAsync(request.JobPostingId, threshold, new PageRequest(request.Page, request.PageSize), ct);
    }
}
