using JobPlatform.AiMatching.Application.DTOs.Matching;
using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.AiMatching.Domain.Interfaces.Repositories;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Interfaces.Ports;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

internal sealed class GetJobMatchRankingHandler(IKnownProfileRepository profiles, IMatchReadStore store, IMatchingConfigurationProvider configuration, ICurrentUser user)
    : IQueryHandler<GetJobMatchRankingQuery, PagedResult<MatchedJobDto>>
{
    public async Task<Result<PagedResult<MatchedJobDto>>> Handle(GetJobMatchRankingQuery request, CancellationToken ct)
    {
        var page = new PageRequest(request.Page, request.PageSize);
        var profile = user.UserId is { } account ? await profiles.GetByOwnerAsync(account, ct) : null;
        if (profile is null)
        {
            return PageMapping.Of(Array.Empty<MatchedJobDto>(), page, 0); // no profile yet: nothing qualifies, which is a valid (empty) result
        }

        var threshold = (await configuration.GetAsync(ct)).MatchThresholdPercent;
        return await store.ListJobRankingAsync(profile.Id, Math.Max(threshold, request.MinScore ?? 0m), page, ct);
    }
}
