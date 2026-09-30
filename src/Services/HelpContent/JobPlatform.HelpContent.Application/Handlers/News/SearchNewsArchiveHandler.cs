using JobPlatform.HelpContent.Application.DTOs.News;
using JobPlatform.HelpContent.Application.Interfaces;
using JobPlatform.HelpContent.Application.Queries.News;
using JobPlatform.SharedKernel.Application.Interfaces.Cqrs;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.News;

internal sealed class SearchNewsArchiveHandler : IQueryHandler<SearchNewsArchiveQuery, PagedResult<NewsListItemView>>
{
    private readonly IHelpContentReadStore _store;

    public SearchNewsArchiveHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<PagedResult<NewsListItemView>>> Handle(SearchNewsArchiveQuery request, CancellationToken ct) =>
        await _store.SearchNewsArchiveAsync(request.Keyword, new PageRequest(request.Page, request.PageSize), ct);
}
