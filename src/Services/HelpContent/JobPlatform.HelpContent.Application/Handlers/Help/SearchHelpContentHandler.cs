using JobPlatform.HelpContent.Application.DTOs.Help;
using JobPlatform.HelpContent.Application.Queries.Help;
using JobPlatform.SharedKernel.Application.Abstractions;
using JobPlatform.SharedKernel.Application.Paging;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.HelpContent.Application.Handlers.Help;

internal sealed class SearchHelpContentHandler : IQueryHandler<SearchHelpContentQuery, PagedResult<HelpSearchResultView>>
{
    private readonly IHelpContentReadStore _store;

    public SearchHelpContentHandler(IHelpContentReadStore store) => _store = store;

    public async Task<Result<PagedResult<HelpSearchResultView>>> Handle(SearchHelpContentQuery request, CancellationToken ct) =>
        await _store.SearchHelpContentAsync(request.Keyword, request.Role, new PageRequest(request.Page, request.PageSize), ct);
}
