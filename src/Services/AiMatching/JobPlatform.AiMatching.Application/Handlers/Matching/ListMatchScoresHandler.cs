using JobPlatform.AiMatching.Application.Queries.Matching;
using JobPlatform.SharedKernel.ApiContracts.AiMatching;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.AiMatching.Application.Handlers.Matching;

internal sealed class ListMatchScoresHandler(IMatchReadStore store, IMatchingConfigurationProvider configuration)
    : IQueryHandler<ListMatchScoresQuery, MatchScoreListDto>
{
    public async Task<Result<MatchScoreListDto>> Handle(ListMatchScoresQuery request, CancellationToken ct)
    {
        var config = await configuration.GetAsync(ct);
        var page = await store.ListScoresWithBreakdownAsync(request.JobPostingId, request.MinScore ?? 0m, new PageRequest(request.Page, request.PageSize), ct);
        return new MatchScoreListDto(page.Items, page.Page, page.PageSize, page.TotalCount, config.MatchThresholdPercent, config.ConfigVersion.ToString());
    }
}
