using JobPlatform.AiMatching.Application.Interfaces;
using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

internal sealed class GetJobMatchRankingForProfileHandler(IMatchReadStore store, IMatchingConfigurationProvider configuration)
    : IQueryHandler<GetJobMatchRankingForProfileQuery, MatchRankingDto>
{
    public async Task<Result<MatchRankingDto>> Handle(GetJobMatchRankingForProfileQuery request, CancellationToken ct)
    {
        var threshold = (await configuration.GetAsync(ct)).MatchThresholdPercent;
        var page = new PageRequest(request.Page, request.PageSize);
        var result = await store.ListJobRankingAsync(request.ProfileId, threshold, page, ct);
        return new MatchRankingDto(result.Items.Select(i => new MatchRankingItemDto(i.JobPostingId, i.Title, i.Score, i.ComputedAtUtc)).ToArray(), result.Page,
            result.PageSize, result.TotalCount, threshold);
    }
}
